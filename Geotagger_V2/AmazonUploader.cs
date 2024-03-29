﻿using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        public static bool Intialise(int count)
        {
            try
            {
                s3Client = new AmazonS3Client();
                fileQueue = new ConcurrentQueue<string>();
                errorQueue = new BlockingCollection<string>();
                semaphoreCount = count;
                _pool = new Semaphore(0, count);
                uploadSum = 0;
                return true;
            } catch (Exception ex)
            {
                string message = $"Error: {ex.Message}";
                string caption = $"Error";
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                DialogResult cancel;
                cancel = MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
                return false;
            }
            
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
                try
                {
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
                            taskList.Add(new Task(() => UploadFiles(amazonPath)));
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
                            Interlocked.Exchange(ref _progressMessage, "Uploaded..... ");
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
                        string message = $"Bucket does not exist - contact administrator";
                        string caption = $"Bucket error";
                        MessageBoxButtons buttons = MessageBoxButtons.OK;
                        DialogResult cancel;
                        cancel = MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
                    }
                } catch (AmazonS3Exception amazonS3Exception)
                {
                    string message = $"Could not connect to bucket - contact administrator {amazonS3Exception.Message}";
                    string caption = $"{amazonS3Exception.ErrorCode}";
                    MessageBoxButtons buttons = MessageBoxButtons.OK;
                    DialogResult cancel;
                    cancel = MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
                } catch (Exception ex)
                {
                    string message = $"An unknow error occured - contact administrator {ex.Message}";
                    string caption = $"{ex.Message}";
                    MessageBoxButtons buttons = MessageBoxButtons.OK;
                    DialogResult cancel;
                    cancel = MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
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
        private static async void UploadFiles(string bucketName)
        {
            _pool.WaitOne();
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
                        CannedACL = S3CannedACL.PublicRead,
                    };
                    string base64 = checkMD5(path);
                    using (FileStream stream = new FileStream(path, FileMode.Open))
                    {
                        putRequest.MD5Digest = base64;
                        putRequest.InputStream = stream;
                        putRequest.ContentType = "image/jpg";
                        PutObjectResponse response = await s3Client.PutObjectAsync(putRequest);
                    }
                    Interlocked.Add(ref uploadSum, 1);
                    double newValue = ((double)uploadSum / (double)files) * 100;
                    Interlocked.Exchange(ref _progressValue, newValue);
                }
                catch (AmazonS3Exception amazonS3Exception)
                {
                    if (amazonS3Exception.ErrorCode != null) {
                        if (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                        ||
                        amazonS3Exception.ErrorCode.Equals("InvalidSecurity"))
                        {
                            string message = $"Invalid login credentials {amazonS3Exception.Message}";
                            string caption = $"{amazonS3Exception.ErrorCode}";
                            MessageBoxButtons buttons = MessageBoxButtons.OK;
                            DialogResult cancel;
                            cancel = MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
                        }
                        else if (amazonS3Exception.ErrorCode.Equals("AccessDenied")) {
                            string message = $"Bucket permission Error {amazonS3Exception.Message}";
                            string caption = $"{amazonS3Exception.ErrorCode}";
                            MessageBoxButtons buttons = MessageBoxButtons.OK;
                            DialogResult cancel;
                            cancel = MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
                        }
                        else
                        {
                            string message = $"Unknown Amazon error {amazonS3Exception.Message}";
                            string caption = $"{amazonS3Exception.ErrorCode}";
                            MessageBoxButtons buttons = MessageBoxButtons.OK;
                            DialogResult cancel;
                            cancel = MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        string message = $"An unknown error occured {amazonS3Exception.Message}";
                        string caption = $"{amazonS3Exception.ErrorCode}";
                        MessageBoxButtons buttons = MessageBoxButtons.OK;
                        DialogResult cancel;
                        cancel = MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
                        
                    }
                    break;
                }
                catch (Exception e)
                {
                    errorQueue.Add(path);
                    fileQueue.Enqueue(path); //retry file upload
                    Console.WriteLine(
                        "Unknown encountered on server. Message:'{0}' when writing an object"
                        , e.Message);
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
