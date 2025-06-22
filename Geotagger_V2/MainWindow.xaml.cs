using Amazon.S3.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Geotagger_V2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string mDBPath;
        private string mInputPath;
        private string mOutputPath;
        private DispatcherTimer dispatcherTimer;
        private GTWriter manager;
        private GTReader reader;
        private Stopwatch stopwatch;
        private bool timer = false;
        private int startCount = 0;
        private bool writeMode; //write or read mode
        private bool uploading = false; //write or read mode
        private int prevUploadCount = 0;
        private string bucket;
        private string prefix;

    
        public MainWindow()
        {
            InitializeComponent();
            ProgressBar2.Visibility = Visibility.Hidden;
            ProgressText2.Visibility = Visibility.Hidden;
            txtBoxDB.IsReadOnly = true;
            txtBoxInput.IsReadOnly = true;
            txtBoxOutput.IsReadOnly = true;
            writeMode = true;
            txtBoxDB.Text = mDBPath = Properties.Settings.Default.AccessDB;
            txtBoxOutput.Text = mOutputPath = Properties.Settings.Default.AmazonFolder;
            //TabItemRead.IsEnabled = false;
        }

        private void BrowseInput_Button_Click(object sender, RoutedEventArgs e)
        {
            using (var browseFolderDialog = new FolderBrowserDialog())
            {
                browseFolderDialog.SelectedPath = Properties.Settings.Default.RootFolder;
                browseFolderDialog.ShowNewFolderButton = false;
                DialogResult result = browseFolderDialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(browseFolderDialog.SelectedPath))
                {
                    txtBoxInput.Text = mInputPath = browseFolderDialog.SelectedPath;

                }
                if(File.Exists(mDBPath) && Directory.Exists(mInputPath) && Directory.Exists(mOutputPath))
                    {
                    Geotag.IsEnabled = true;
                } else
                {
                    Geotag.IsEnabled = false;
                }
            }
            if (bucket == null  || prefix == null)
            {
                setAmazonBucketFromAccess();
            }
        }

        //private bool FolderHasPhotos(string ProcessingDirectory)
        //{
        //    DirectoryInfo di = new DirectoryInfo(ProcessingDirectory);
        //    FileInfo[] JPGFiles = di.GetFiles("*.jpg");
        //    if (JPGFiles.Length <= 0)
        //    {
        //        return false;
        //    } else
        //    {
        //        return true;
        //    }
        //}

        private void BrowseOutput_Button_Click(object sender, RoutedEventArgs e)
        {
            using (var browseFolderDialog = new FolderBrowserDialog())
            {
                browseFolderDialog.SelectedPath = Properties.Settings.Default.RootFolder;
                DialogResult result = browseFolderDialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(browseFolderDialog.SelectedPath))
                {
                    txtBoxOutput.Text = mOutputPath = browseFolderDialog.SelectedPath;
                    if (File.Exists(mDBPath) && Directory.Exists(mInputPath) && Directory.Exists(mOutputPath))
                    {
                        Geotag.IsEnabled = true;
                            Upload.IsEnabled = true;
                            if (bucket == null || prefix == null)
                            {
                                setAmazonBucketFromAccess();
                            }
                    }
                }
            }
        }
        

        private void SavePersistentValue(string key, string value)
        {
            if (key == "access")
            {
                Properties.Settings.Default.AccessDB = value;
                
            } else if (key == "root")
            {
                Properties.Settings.Default.RootFolder = value;
            }
            Properties.Settings.Default.Save();
        }

        private void setAmazonBucketFromAccess()
        {
            string bucketQuery = "SELECT Config.bucket FROM Config;";
            string prefixQuery = "SELECT Config.prefix FROM Config;";
            if (mDBPath != null)
            {
                string[] bucketArr = queryDB(bucketQuery, mDBPath);
                string[] prefixArr = queryDB(prefixQuery, mDBPath);
                if (bucketArr[0] != null || prefixArr[0] != null)
                {
                    string caption = "Error";
                    MessageBoxButtons buttons = MessageBoxButtons.OK;
                    System.Windows.Forms.MessageBox.Show(bucketArr[0], caption, buttons, MessageBoxIcon.Error);
                    bucketLabel.Content = $"Bucket: Error";
                }
                else
                {
                    bucket = bucketArr[1];
                    prefix = prefixArr[1];
                    string[] tokens = prefix.Split('/');
                    for (int i = 0; i < tokens.Length; i++) 
                    {
                        int index = tokens[i].IndexOf('_');
                        if (index != -1)
                        {
                            string newToken  = tokens[i].Insert(index, "_");
                            tokens[i] = newToken;
                        }
                    }
                    bucketLabel.Content = $"Bucket: {bucket}";
                    prefixLabel.Content = $"Prefix: {String.Join("/", tokens)}";
  
                    
                }
            }
        }

        private void BrowseDB_Button_Click(object sender, RoutedEventArgs e)
        {
            if (TabItemWrite.IsSelected)
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Title = "Import Access Database";
                    openFileDialog.Filter = "MS Access (*.mdb *.accdb)|*.mdb;*.accdb";
                    openFileDialog.RestoreDirectory = true;
                    DialogResult result = openFileDialog.ShowDialog();
                    if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(openFileDialog.FileName))
                    {
                        txtBoxDB.Text = mDBPath = openFileDialog.FileName;
                        SavePersistentValue("access", openFileDialog.FileName);
                        string rootDirectory = Directory.GetParent(openFileDialog.FileName).FullName;
                        SavePersistentValue("root", rootDirectory);
                        setAmazonBucketFromAccess();
                    }
                    else
                    {
                        txtBoxDB.Text = mDBPath = openFileDialog.FileName;
                        SavePersistentValue("access", "");
                    }
                }
            }
        }

        private void BrowseInputRead_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog browseFolderDialog = new FolderBrowserDialog())
            {
                browseFolderDialog.ShowDialog();
                if (browseFolderDialog.SelectedPath != "")
                {
                    txtInputPathRead.Text = mInputPath = browseFolderDialog.SelectedPath;
                }
            }
        }

        private void BrowseOutputRead_Click(object sender, RoutedEventArgs e)
        {
            using (SaveFileDialog saveFileDialog1 = new SaveFileDialog())
            {
                saveFileDialog1.Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*";
                saveFileDialog1.ShowDialog();
                if (saveFileDialog1.FileName != "")
                {
                    txtOutputPathRead.Text = mOutputPath = saveFileDialog1.FileName;
                }
            }
        }

        private async void GeotagRead_Click(object sender, RoutedEventArgs e)
        {
            if (Utilities.directoryHasFiles(mInputPath)) {
                reader = GTReader.Instance();
                startTimers();
                BrowseInputRead.IsEnabled = false;
                BrowseOutputRead.IsEnabled = false;
                GeotagRead.IsEnabled = false;
                TabItemWrite.IsEnabled = false;
                manager = null;
                bool format = Utilities.isFileValid(mOutputPath);

                if (format)
                {
                    var source = new CancellationTokenSource();
                    CancellationToken token = source.Token;
                    Task worker = Task.Factory.StartNew(() =>
                    {
                        showProgressBar();
                        progressIndeterminate(true);
                        reader.photoReader(mInputPath, false);
                        progressIndeterminate(false);
                        TaskStatus result = reader.readGeotag().Result;
                        if (result == TaskStatus.RanToCompletion)
                        {

                        }
                    });
                    await Task.WhenAll(worker);
                    ConcurrentQueue<Record> queue = reader.Queue;
                    List<Record> list = queue.ToList();
                    Writer writer = new Writer(list);
                    writer.WriteCSV(mOutputPath);
                }
                else
                {
                    string message = "The output file path is not valid";
                    string caption = "Error in output path";
                    MessageBoxButtons buttons = MessageBoxButtons.OK;
                    // Displays the MessageBox.
                    System.Windows.Forms.MessageBox.Show(message, caption, buttons);
                }
            } 
            
        }

        private void startTimers()
        {
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(DispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 1);
            dispatcherTimer.Start();
            Timer();
        }

        private void startTimers(int milliseconds)
        {
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(DispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, milliseconds);
            dispatcherTimer.Start();
            Timer();
        }

        /// <summary>
        /// Fired when user clicks geotag button. Starts geotagger writer
        /// </summary>
        /// <param name="sender">object - the geotag button</param>
        /// <param name="e">click event</param>
        private async void Geotag_Click(object sender, RoutedEventArgs e)
        {
            if (Utilities.directoryHasFiles(mInputPath) || mInputPath != null)
            {
                manager = GTWriter.Instance(50);
                manager.Initialise();
                refreshUI();
                startTimers();
                BrowseDB.IsEnabled = false;
                BrowseInput.IsEnabled = false;
                BrowseOutput.IsEnabled = false;
                Geotag.IsEnabled = false;
                TabItemRead.IsEnabled = false;
                Upload.IsEnabled = false;
                reader = null;
                var source = new CancellationTokenSource();
                CancellationToken token = source.Token;
                Task worker = Task.Factory.StartNew(() =>
                {
                    showProgressBar();
                    progressIndeterminate(true);
                    manager.photoReader(mInputPath, false);
                    progressIndeterminate(false);
                    TaskStatus result = manager.readDatabase(mDBPath, "").Result;
                    if (result == TaskStatus.RanToCompletion)
                    {
                        if (manager.updateDuplicateCount == 0)
                        {
                            TaskStatus consumerStatus = manager.writeGeotag(mOutputPath).Result;
                            if (consumerStatus == TaskStatus.RanToCompletion)
                            {
                                Dispatcher.Invoke((Action)(() =>
                                {
                                    refreshUI();
                                }));
                            }
                            else
                            {
                                Console.WriteLine(consumerStatus);
                            }
                        }
                        else
                        {
                            MessageBoxResult msgResult = System.Windows.MessageBox.Show("Duplicates detected in the record database",
                                                "Operation will be cancelled",
                                                MessageBoxButton.OK,
                                                MessageBoxImage.Error);
                            source.Cancel();

                        }
                        if (token.IsCancellationRequested)
                        {
                            timer = false;
                            token.ThrowIfCancellationRequested();
                        }
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show("An unknown error has occured - Geotag Task failed", "Geotag Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                        
                    }
                }, token);

                try
                {
                    await Task.WhenAll(worker);
                    Dispatcher.Invoke((Action)(() =>
                    {
                        refreshUI();
                        SpeedLabel.Content = "Items/sec: 0";
                    }));
                    BrowseDB.IsEnabled = true;
                    BrowseInput.IsEnabled = true;
                    BrowseOutput.IsEnabled = true;
                    Geotag.IsEnabled = false;
                    Upload.IsEnabled = true;
                    dispatcherTimer.Stop();
                    timer = false;
                    TimeSpan ts = stopwatch.Elapsed;
                    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                    LogWriter log = new LogWriter(manager);
                    log.Write(mDBPath, elapsedTime);
                    if (manager.getGeotagCount() == 0)
                    {
                        System.Windows.Forms.MessageBox.Show("No photos where geotagged!\nCheck that the correct database has been selected and/or the correct photo folder has been selected.", "Geotag Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    manager.updateProgessMessage = "Cancelled";
                    refreshUI();
                }          
            }
        }

        /// <summary>
        /// Fired every second updates UI
        /// </summary>
        /// <param name="sender">Dispatach Timer</param>
        /// <param name="args"></param>
        public void DispatcherTimer_Tick(object sender, EventArgs args)
        {
            if (writeMode)
            {
                if (uploading)
                {
                    refreshUI();
                } else
                {
                    int geotagCount = manager.updateGeoTagCount;
                    int count = geotagCount - startCount;
                    SpeedLabel.Content = "Items/sec: " + count;
                    startCount = geotagCount;
                    refreshUI();
                }
            }         
            else
            {
                refreshUI();
            }                 
        }

        private void refreshUI()
        {
            if (uploading)
            {
                int uploaded = AmazonUploader.uploadSum - prevUploadCount;
                if (uploaded != 0)
                {
                    prevUploadCount = AmazonUploader.uploadSum;
                    SpeedLabel.Content = "Items/sec: " + uploaded;
                    UploadErrorLabel.Content = "Photo Name Errors: " + AmazonUploader.errorQueue.Count;
                    FilesToUploadLabel.Content = "Files to Upload: " + AmazonUploader.files;
                    UploadCountLabel.Content = "Uploading File: " + AmazonUploader.uploadSum + " of " + AmazonUploader.files;
                }
                ProgressBar1.Value = AmazonUploader.updateProgessValue;
                int progress = Convert.ToInt32(ProgressBar1.Value);
                ProgressText.Text = progress.ToString() + "%";
                ProgressLabel.Content = AmazonUploader.updateProgessMessage;
            } else
            {
                if (writeMode)
                {
                    ProgressLabel.Content = manager.updateProgessMessage;
                    int progress = Convert.ToInt32(manager.updateProgessValue);
                    ProgressText.Text = progress.ToString() + "%";
                    ProgressBar1.Value = manager.updateProgessValue;
                    PhotoCountLabel.Content = "Processing photo: " + (manager.updatePhotoCount - manager.updatePhotoQueueCount) + " of " + manager.updatePhotoCount;
                    RecordCountLabel.Content = "Records to process: " + manager.updateRecordCount;
                    GeotagLabel.Content = "Geotag Count: " + manager.updateGeoTagCount;
                    RecordDictLabel.Content = "Record Dictionary: " + manager.updateRecordDictCount;
                    PhotoQueueLabel.Content = "Photo Queue: " + manager.updatePhotoQueueCount;
                    BitmapQueueLabel.Content = "Bitmap Queue: " + manager.updateBitmapQueueCount;
                    NoRecordLabel.Content = "Photos with no record: " + manager.updateNoRecordCount;
                    DuplicateLabel.Content = "Duplicate Records: " + manager.updateDuplicateCount;
                    PhotoErrorLabel.Content = "Photo Name Errors: " + manager.updatePhotoNameError;
                }
                else
                {
                    ProgressLabel2.Content = reader.updateProgessMessage;
                    ProgressBar2.Value = reader.updateProgessValue;
                    PhotoCountLabelReader.Content = "Processing photo: " + reader.updateRecordQueueCount + " of " + reader.updatePhotoCount;
                    ErrorLabelReader.Content = "Errors: " + reader.updateErrorCount;
                }
            }      
        }

        /// <summary>
        /// Starts program timer on its own thread and display output on main form
        /// </summary>
        private void Timer()
        {
            timer = true;
            stopwatch = new Stopwatch();
            stopwatch.Start();
            System.Windows.Controls.Label timeLabel;
            if (writeMode)
            {
                timeLabel = TimeLabel;
            } else
            {
                timeLabel = TimeLabelReader;
            }
            Task sw = Task.Factory.StartNew(() =>
            {
                if (writeMode)
                {
                    timeLabel = TimeLabel;
                }
                else
                {
                    timeLabel = TimeLabelReader;
                }
                while (timer)
                {  
                    TimeSpan ts = stopwatch.Elapsed;
                    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                    Dispatcher.Invoke((Action)(() =>
                    {
                        timeLabel.Content = elapsedTime;
                    }));
                    Thread.Sleep(10);
                }
            });
        }

        private void showProgressBar()
        {
            Dispatcher.Invoke((Action)(() => {
                if (writeMode)
                {
                    ProgressBar1.Visibility = Visibility.Visible;
                    ProgressText.Visibility = Visibility.Visible;
                    ProgressLabel.Visibility = Visibility.Visible;
                } else {
                {
                    ProgressBar2.Visibility = Visibility.Visible;
                    ProgressText2.Visibility = Visibility.Visible;
                    ProgressLabel2.Visibility = Visibility.Visible;
                }
                }
            }));
        }

        /// <summary>
        /// Sets or unsets whether progress bar is undertimanate
        /// </summary>
        /// <param name="isIndeterminate"></param>
        private void progressIndeterminate(bool isIndeterminate)
        {
            Dispatcher.Invoke((Action)(() => {
                if (isIndeterminate)
                {
                    if (writeMode)
                    {
                        ProgressBar1.IsIndeterminate = true;
                    } else
                    {
                        ProgressBar2.IsIndeterminate = true;
                    }
                }
                else
                {
                    if (writeMode)
                    {
                        ProgressBar1.IsIndeterminate = false;
                    } else
                    {
                        ProgressBar2.IsIndeterminate = false;
                    }
                }

            }));
        }

        private void changeMode(object sender, RoutedEventArgs e)
        {
            writeMode = !writeMode;

        }

        private void txtOutputPathRead_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Console.WriteLine(txtOutputPathRead.Text);
            mOutputPath = txtOutputPathRead.Text;
        }

        private string[] queryDB(string query, string dbPath)
        {
            string connectionString = string.Format("Provider={0}; Data Source={1}; Jet OLEDB:Engine Type={2}",
               "Microsoft.Jet.OLEDB.4.0", dbPath, 5);
            OleDbConnection connection = new OleDbConnection(connectionString);
            connection.Open();
            OleDbCommand command = new OleDbCommand(query, connection);
            object[] row = null;
            try
            {
                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        row = new object[reader.FieldCount];
                        reader.GetValues(row);
                    }
                    command.Dispose();
                }
                connection.Close();
                string result = (string)row[0]; ;
                return new string[] {null, result};

            } catch (Exception ex)
            {
                connection.Close();
                return new string[] { ex.Message, null }; ;
            }         
        }

        private void reset()
        {
            try
            {
                dispatcherTimer.Stop();
                timer = false;
                Dispatcher.Invoke((Action)(() =>
                {
                    refreshUI();
                    uploading = false;
                    Geotag.IsEnabled = false;
                    Upload.IsEnabled = false;
                    SpeedLabel.Content = "Items/sec: 0";
                }));
                
            }
            catch { }
        }

        private void deletePhotos()
        {
            string message = "Would you like to delete the uploaded photos from your local directory";
            string caption = "Delete Photos";
            MessageBoxButtons buttons = MessageBoxButtons.YesNo;
            DialogResult result = System.Windows.Forms.MessageBox.Show(new Form() { TopMost = true }, message, caption, buttons, MessageBoxIcon.Question);
            if (result == System.Windows.Forms.DialogResult.Yes) {
                try
                {
                    if (Directory.Exists(mOutputPath)) {
                        Dispatcher.Invoke((Action)(() =>
                        {
                            Geotag.IsEnabled = false;
                            Upload.IsEnabled = false;
                            ProgressLabel.Content = "Deleting.....";
                            ProgressBar1.Value = 0;
                        }));
                        string[] files = Directory.GetFiles(mOutputPath);
                        for (int i = 0; i < files.Length; i++)
                        {
                            File.Delete(files[i]);
                            double progress = (i / (double)files.Length) * 100;
                            int progressValue = (int)Math.Ceiling(progress);
                            Dispatcher.Invoke((Action)(() =>
                            {
                                ProgressBar1.Value = progress;
                                ProgressText.Text = progressValue.ToString() + "%";
                            }));
                        }
                        //Directory.Delete(mOutputPath);
                    }
                } catch (Exception ex)
                {
                    string errMessage = ex.Message;
                    string errCaption = "Error";
                    MessageBoxButtons errButtons = MessageBoxButtons.OK;
                    System.Windows.Forms.MessageBox.Show(errMessage, errCaption, errButtons, MessageBoxIcon.Error);
                }
            } 
        }

        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            uploading = true;
            refreshUI();
            Geotag.IsEnabled = false;
            Upload.IsEnabled = false;
            string targetDirectory = mOutputPath;
            if (Utilities.directoryHasFiles(mOutputPath))
            {
                startTimers(500);
                Task upload = Task.Factory.StartNew(() =>
                {
                    showProgressBar();
                    progressIndeterminate(false);
                    //ProgressBar1.SetState(2);
                    Console.WriteLine("Processor Count:" + Environment.ProcessorCount);
                    bool start = AmazonUploader.Intialise(Environment.ProcessorCount);
                    if (start)
                    {
                        Task uploader = Task.Factory.StartNew(() =>
                             AmazonUploader.Upload(targetDirectory, bucket, prefix));
                        uploader.Wait();
                        reset();
                        deletePhotos();
                        Dispatcher.Invoke((Action)(() =>
                        {
                            ProgressLabel.Content = "Upload Completed!";
                        }));
                    } else
                    {
                        reset();
                    }
                    
                });
            
            } else
            {
                string message = "The selected folder contains no files. Please re-select folder";
                string caption = "No files dectected";
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                DialogResult result;
                result = System.Windows.Forms.MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
            }         
        }


    }

}
