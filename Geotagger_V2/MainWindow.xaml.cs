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
        private string mInputReadPath;
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
            //ProgressBar1.Visibility = Visibility.Hidden;
            //ProgressText.Visibility = Visibility.Hidden;
            ProgressBar2.Visibility = Visibility.Hidden;
            ProgressText2.Visibility = Visibility.Hidden;
            writeMode = true;
        }

        private void BrowseDB_Button_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button b = sender as System.Windows.Controls.Button;
            if (b.Name == "BrowseDB")
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Title = "Import Access Database";
                openFileDialog.Filter = "MS Access (*.mdb *.accdb)|*.mdb;*.accdb";
                openFileDialog.RestoreDirectory = true;
                openFileDialog.ShowDialog();
                
                if (mDBPath != "")
                {
                    txtBoxDB.Text = mDBPath = openFileDialog.FileName;
                    string bucketQuery = "SELECT Config.bucket FROM Config;";
                    string prefixQuery = "SELECT Config.prefix FROM Config;";
                    if (mDBPath != null)
                    {
                        bucket = queryDB(bucketQuery, mDBPath);
                        prefix = queryDB(prefixQuery, mDBPath);
                    }
                }
                else
                {
                    Console.WriteLine("cancel");
                }
            } else 
            {
                if (TabItemWrite.IsSelected) //write
                {
                    FolderBrowserDialog browseFolderDialog = new FolderBrowserDialog();
                    browseFolderDialog.ShowDialog();
                    if (b.Name == "BrowseInput")
                    {
                        if (browseFolderDialog.SelectedPath != "")
                        {
                            txtBoxInput.Text = mInputPath = browseFolderDialog.SelectedPath;
                            
                        }
                    }
                    else
                    {
                        if (browseFolderDialog.SelectedPath != "")
                        {
                            txtBoxOutput.Text = mOutputPath = browseFolderDialog.SelectedPath;
                            Upload.IsEnabled = true;
                            Geotag.IsEnabled = true;
                        }
                    }
                } else //read
                {
                    if (b.Name == "BrowseInputRead")
                    {
                        FolderBrowserDialog browseFolderDialog = new FolderBrowserDialog();
                        browseFolderDialog.ShowDialog();
                        if (browseFolderDialog.SelectedPath != "")
                        {
                            txtInputPathRead.Text = mInputPath = browseFolderDialog.SelectedPath;
                        }
                    } else
                    {
                        SaveFileDialog saveFileDialog1 = new SaveFileDialog();
                        saveFileDialog1.Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*";
                        saveFileDialog1.ShowDialog();
                        if (saveFileDialog1.FileName != "")
                        {
                            txtOutputPathRead.Text = mOutputPath = saveFileDialog1.FileName;
                        }
                    }                 
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
                startTimers();
                BrowseDB.IsEnabled = false;
                BrowseInput.IsEnabled = false;
                BrowseOutput.IsEnabled = false;
                Geotag.IsEnabled = false;
                TabItemRead.IsEnabled = false;
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
                    Console.WriteLine(result);
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
                        Console.WriteLine(result);
                    }
                }, token);

                try
                {
                    await Task.WhenAll(worker);
                }
                catch (OperationCanceledException ex)
                {
                    manager.updateProgessMessage = "Cancelled";
                    refreshUI();
                }
                BrowseDB.IsEnabled = true;
                BrowseInput.IsEnabled = true;
                BrowseOutput.IsEnabled = true;
                Geotag.IsEnabled = true;
                TabItemRead.IsEnabled = true;
                dispatcherTimer.Stop();
                timer = false;
                TimeSpan ts = stopwatch.Elapsed;
                string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                LogWriter log = new LogWriter(manager);
                log.Write(mDBPath, elapsedTime);
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
                ProgressLabel.Content = AmazonUploader.updateProgessMessage;
            } else
            {
                if (writeMode)
                {
                    ProgressLabel.Content = manager.updateProgessMessage;
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

        //private void hideProgressBar()
        //{
        //    Dispatcher.Invoke((Action)(() => {
        //        if (writeMode)
        //        {
        //            ProgressBar1.Visibility = Visibility.Hidden;
        //            ProgressText.Visibility = Visibility.Hidden;
        //            ProgressLabel.Visibility = Visibility.Hidden;
        //        } else
        //        {
        //            ProgressBar2.Visibility = Visibility.Hidden;
        //            ProgressText2.Visibility = Visibility.Hidden;
        //            ProgressLabel2.Visibility = Visibility.Hidden;
        //        }
        //    }));
        //}


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
            if (writeMode)
            {
                writeMode = false;
            }
            else
            {
                writeMode = true;
            }
            Console.WriteLine(writeMode);
        }

        private void txtOutputPathRead_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Console.WriteLine(txtOutputPathRead.Text);
            mOutputPath = txtOutputPathRead.Text;
        }

        private string queryDB(string query, string dbPath)
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
            } catch (Exception ex)
            {
                string caption = "Error";
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                System.Windows.Forms.MessageBox.Show(ex.Message, caption, buttons);
            }
            
            connection.Close();
            string result = null;
            try
            {
                result = (string)row[0];
            } catch (Exception e)
            {
                Console.WriteLine($"An exception occured: {e.Message}");
            }
            return result;
        }

        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            uploading = true;
            string targetDirectory = mOutputPath;
            if (Utilities.directoryHasFiles(mOutputPath))
            {
                startTimers(500);
                Amazon amazon = new Amazon(Environment.ProcessorCount);
                Task upload = Task.Factory.StartNew(() =>
                {
                    showProgressBar();
                    progressIndeterminate(false);
                    Console.WriteLine("Processor Count:" + Environment.ProcessorCount);
                    AmazonUploader.Intialise(Environment.ProcessorCount);
                    Task t = Task.Factory.StartNew(() =>
                        AmazonUploader.Upload(targetDirectory, bucket, prefix)
                    );
                    Task.WhenAll(t);
                    try
                    {
                        t.Wait();
                        uploading = false;
                        dispatcherTimer.Stop();
                        prevUploadCount = 0;
                        Dispatcher.Invoke((Action)(() =>
                        {
                            SpeedLabel.Content = "Items/sec: 0";
                        }));
                        timer = false;
                    }
                    catch { }
                });
            } else
            {
                //alert no files
                string message = "The selected folder contains no files. Please re-select folder";
                string caption = "No files dectected";
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                DialogResult result;

                // Displays the MessageBox.
                result = System.Windows.Forms.MessageBox.Show(message, caption, buttons, MessageBoxIcon.Error);
            }
            
            
        }
    }
}
