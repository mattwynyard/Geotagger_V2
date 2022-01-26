using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Geotagger_V2
{
    public class Amazon
    {
        private IAmazonS3 s3Client;
        private ConcurrentQueue<string> fileQueue;
        private int uploadSum = 0;
        private BlockingCollection<string> errorQueue;
        private Semaphore _pool;
        private int semaphoreCount;
        private DispatcherTimer dispatcherTimer;
        private string _progressMessage;
        public double _progressValue;
        private int files;

        public Amazon(int count)
        {
            s3Client = new AmazonS3Client();
            fileQueue = new ConcurrentQueue<string>();
            errorQueue = new BlockingCollection<string>();
            semaphoreCount = count;
            _pool = new Semaphore(0, count);
        }

        public void Dispose()
        {
            s3Client.Dispose();
            s3Client = null;
            fileQueue = null;
            errorQueue = null;
            uploadSum = 0;
        }
        public void Upload(string directory, string bucket, string prefix)
        {
            if (s3Client != null)
            {
                string[] filePaths = Directory.GetFiles(directory);
                string amazonPath = bucket + "/" + prefix;
                var listResponse = s3Client.ListObjectsV2(new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = prefix
                });
                if (listResponse.S3Objects.Count > 0) //test if folder exists => move
                {
                    List<Task> taskList = new List<Task>();
                    foreach (string filePath in filePaths)
                    {
                        fileQueue.Enqueue(filePath);
                    }
                    for (int i = 0; i < semaphoreCount + 1; i++) //+ 1 thread that blocks t until all workers finish
                    {
                        taskList.Add(new Task(() => UploadFile(amazonPath)));
                    }
                    Task[] tasks = taskList.ToArray();
                    Console.WriteLine("Files: " + fileQueue.Count);
                    files = fileQueue.Count;
                    var watch = Stopwatch.StartNew();
                    _pool.Release(semaphoreCount);
                    try
                    {
                        foreach (Task task in tasks)
                        {
                            task.Start();
                        }
                    }
                    catch (AggregateException ex)
                    {
                        Console.WriteLine("An action has thrown an exception. THIS WAS UNEXPECTED.\n{0}", ex.InnerException.ToString());
                    }
                    Task.WaitAll(tasks);
                    watch.Stop();
                    Console.WriteLine($"Time Taken: { watch.ElapsedMilliseconds} ms.");
                    Console.WriteLine($"Files uploaded: {uploadSum}");
                    Console.WriteLine($"Errors: {errorQueue.Count}");
                }
                else
                {
                    //alert no bucket
                }
            }
            else
            {
                //alert no amazon connection
            }

        }
        private async void UploadFile(string bucketName)
        {
            _pool.WaitOne();
            string result;
            string path;
            while (fileQueue.TryDequeue(out path))
            {
                string fileName = Path.GetFileName(path);
                try
                {
                    PutObjectRequest putRequest = new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = fileName,
                    };
                    string base64 = checkMD5(path);
                    using (FileStream stream = new FileStream(path, FileMode.Open))
                    {
                        putRequest.MD5Digest = base64;
                        putRequest.InputStream = stream;
                        PutObjectResponse response = await s3Client.PutObjectAsync(putRequest);
                        Console.WriteLine(fileName + ": " + response.HttpStatusCode);
                    }
                    Interlocked.Add(ref uploadSum, 1);
                    double newValue = ((double)uploadSum / (double)files) * 100;
                    Interlocked.Exchange(ref _progressValue, newValue);
                }
                catch (AmazonS3Exception amazonS3Exception)
                {
                    if (amazonS3Exception.ErrorCode != null &&
                        (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                        ||
                        amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                    {
                        throw new Exception("Check the provided AWS Credentials.");
                    }
                    else
                    {
                        errorQueue.Add(path);
                        throw new Exception("Error occurred: " + amazonS3Exception.Message);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(
                        "Unknown encountered on server. Message:'{0}' when writing an object"
                        , e.Message);
                    result = e.ToString();
                }

            }
            _pool.Release();
            Console.WriteLine("exiting thread");
        }

        public string checkMD5(string path)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = new FileStream(path, FileMode.Open))
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }
        }

        public double updateProgessValue
        {
            get
            {
                return Interlocked.CompareExchange(ref _progressValue, 0, 0);
            }
        }

        public string updateProgessMessage
        {
            get
            {
                return Interlocked.CompareExchange(ref _progressMessage, "", "");
            }
            set
            {
                Interlocked.Exchange(ref _progressMessage, value);
            }
        }
    }
}

