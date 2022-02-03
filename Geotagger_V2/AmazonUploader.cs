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
using System.Windows.Forms;
using System.Windows.Threading;

namespace Geotagger_V2
{
    public static class AmazonUploader
    {
        private static IAmazonS3 s3Client ;
        private static ConcurrentQueue<string> fileQueue;
        public static int uploadSum = 0;
        public static BlockingCollection<string> errorQueue;
        private static Semaphore _pool;
        private static int semaphoreCount;
        private static string _progressMessage;
        public static double _progressValue;
        public static int files;

        public static void Intialise(int count)
        {
            s3Client = new AmazonS3Client();
            fileQueue = new ConcurrentQueue<string>();
            errorQueue = new BlockingCollection<string>();
            semaphoreCount = count;
            _pool = new Semaphore(0, count);
        }

        public static void Dispose()
        {
            s3Client.Dispose();
            s3Client = null;
            fileQueue = null;
            errorQueue = null;
            uploadSum = 0;
        }
        public static void Upload(string directory, string bucket, string prefix)
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
                        Interlocked.Exchange(ref _progressMessage, "Building queue");
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
                    Interlocked.Exchange(ref _progressMessage, "Uploading..... ");
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
                    Task t = Task.WhenAll(tasks);
                    try
                    {
                        t.Wait();
                        Interlocked.Exchange(ref _progressMessage, "Finished..... ");
                        watch.Stop();
                        Thread.Sleep(1000);
                        Console.WriteLine($"Time Taken: { watch.ElapsedMilliseconds} ms.");
                        Console.WriteLine($"Files uploaded: {uploadSum}");
                        Console.WriteLine($"Errors: {errorQueue.Count}");
                        
                    }
                    catch { }
                    
                }
                else
                {
                    string message = "Could not find the selected amazon bucket - check amazon path";
                    string caption = "Amazon bucket error";
                    MessageBoxButtons buttons = MessageBoxButtons.OK;
                    DialogResult result;
                    result = MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
                }
            }
            else
            {
                string message = "Could not connect to amazon - check login credentials";
                string caption = "Amazon connection error";
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                DialogResult result;
                result = MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
            }
        }
        private static async void UploadFile(string bucketName)
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
                        
                        throw new Exception("Error occurred: " + amazonS3Exception.Message);
                    }
                }
                catch (Exception e)
                {
                    errorQueue.Add(path);
                    fileQueue.Enqueue(path); //retry file upload
                    Console.WriteLine(
                        "Unknown encountered on server. Message:'{0}' when writing an object"
                        , e.Message);
                    result = e.ToString();
                }    
            }
            _pool.Release();
            Console.WriteLine("exiting thread");
        }

        public static string checkMD5(string path)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = new FileStream(path, FileMode.Open))
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }
        }

        public static double updateProgessValue
        {
            get
            {
                return Interlocked.CompareExchange(ref _progressValue, 0, 0);
            }
        }

        public static string updateProgessMessage
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
