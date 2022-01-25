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

namespace Geotagger_V2
{
    public static class AmazonUploader
    {
        private static IAmazonS3 s3Client ;
        private static ConcurrentQueue<string> fileQueue;
        private static int uploadSum = 0;
        private static BlockingCollection<string> errorQueue;
        private static string targetDirectory;
        private static Semaphore _pool;
        private static string bucket;
        private static string prefix;


        public static void Intialise(string directory)
        {
            s3Client = new AmazonS3Client();
            fileQueue = new ConcurrentQueue<string>();
            errorQueue = new BlockingCollection<string>();
            _pool = new Semaphore(0, 4);
            targetDirectory = directory;
        }

        public static void Reset()
        {
            s3Client.Dispose();
            s3Client = null;
            fileQueue = null;
            errorQueue = null;
            targetDirectory = null;
            uploadSum = 0;
    }
        public static void Upload(string bucket, string prefix)
        {  
            if (s3Client != null)
            {
                string[] filePaths = Directory.GetFiles(targetDirectory);
                var listResponse = s3Client.ListObjectsV2(new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = prefix
                });
                if (listResponse.S3Objects.Count > 0) //test if folder exists => move
                {
                    List<Action> actionsArray = new List<Action>();
                    foreach (string filePath in filePaths)
                    {
                        string bucketName = "akl-south-urban/test/1";
                        actionsArray.Add(new Action(() => UploadFile(bucketName)));
                        fileQueue.Enqueue(filePath);
                    }
                    Action[] arrayList = actionsArray.ToArray();
                    Console.WriteLine("Size: " + fileQueue.Count);
                    var watch = Stopwatch.StartNew();
                    _pool.Release(4);
                    Task t = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            Parallel.Invoke(new ParallelOptions
                            {
                                MaxDegreeOfParallelism = 4
                            }, arrayList);
                        }
                        catch (AggregateException ex)
                        {
                            Console.WriteLine("An action has thrown an exception. THIS WAS UNEXPECTED.\n{0}", ex.InnerException.ToString());
                        }
                    });
                    t.Wait();
                    watch.Stop();
                    Console.WriteLine($"Time Taken: { watch.ElapsedMilliseconds} ms.");
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
                        Console.WriteLine(fileName + ": " + response.HttpStatusCode);
                    }
                    Interlocked.Add(ref uploadSum, 1);
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
            Console.WriteLine("exiting thread - uploaded: " + uploadSum.ToString() + " files");
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

    }
}
