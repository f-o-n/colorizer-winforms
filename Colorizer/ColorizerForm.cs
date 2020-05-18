using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using System.Runtime.Serialization.Json;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Colorizer
{
    public partial class ColorizerForm : Form
    {

        public ColorizerForm()
        {
            InitializeComponent();
            SetupControls();
        }

        protected void SetupControls()
        {
            sourceTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            destinationTextBox.Text = sourceTextBox.Text + "\\Colorized";
            apiKeyTextBox.Text = ApiKey;

            toolTip.SetToolTip(statusLabel, "Double-click to copy message to clipboard");
            toolTip.SetToolTip(linkLabel, "Image Colorization API on DeepAi by Jason Antic");
            toolTip.SetToolTip(apiKeyTextBox, "DeepAI API Key - Visit https://deepai.org/ to create one");
        }

        public string ApiKey
        {
            get {

                var apiKey = Properties.Settings.Default["ApiKey"].ToString();
                if (string.IsNullOrEmpty(apiKey))
                {
                    apiKey = "quickstart-QUdJIGlzIGNvbWluZy4uLi4K";
                    ApiKey = apiKey;
                }

                return apiKey;
            }
            set {

                Properties.Settings.Default["ApiKey"] = value;
                Properties.Settings.Default.Save();
            }
        }

        public void ProcessFolder(string inputFolder, string outputFolder)
        {
            var apiKey = ApiKey;
            var inputDir = new DirectoryInfo(inputFolder);
            var files = inputDir.GetFiles()
                .Where(i => MimeMapping.GetMimeMapping(i.FullName).StartsWith("image/")).ToList();
            
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            if (files.Count == 0)
                SetStatusMessage("Source folder does not contain any images");

            SetProgressBar(0, 0, files.Count);

            foreach (var file in files)
            {
                //Get unique file path for new file
                var outputFile = "";
                var count = 0;
                do
                {
                    var nameExtension = count++ == 0 ? "_colorized" : "_colorized" + count;
                    var fileName = Path.GetFileNameWithoutExtension(Path.Combine(outputFolder, file.Name));
                    var outputFileName = fileName + nameExtension + file.Extension;
                    outputFile = Path.Combine(outputFolder, outputFileName);

                } while (File.Exists(outputFile));

                try
                {
                    ColorizeImage(file.FullName, outputFile, apiKey);
                }
                catch (Exception e)
                {
                    SetStatusMessage(e.Message);
                    return;
                }

            }
        }

        public void ColorizeImage(string inputImagePath, string outputImagePath, string apiKey)
        {
            /*DeepAi Library is like 180mb...
            var api = new DeepAI_API(apiKey: apiKey);
            var resp = api.callStandardApi("colorizer", new
            {
                image = File.OpenRead(inputImagePath),
            });*/
            
            using (WebClient client = new WebClient())
            {

                string responseString = "";
                client.Headers.Add("api-key", apiKey);

                try
                {
                    var base64Image = Convert.ToBase64String(File.ReadAllBytes(inputImagePath));
                    var nvCollection = new NameValueCollection() {{ "image", base64Image }};
                    var response = client.UploadValues("https://api.deepai.org/api/colorizer", nvCollection);
                    responseString = Encoding.ASCII.GetString(response);
                }
                catch (WebException e)
                {
                    if (e.Response == null)
                    {
                        SetStatusMessage($@"Upload Error: '{e.Message}'");
                        return;
                    }

                    using (var r = new StreamReader(e.Response.GetResponseStream() ?? throw new InvalidOperationException()))
                    {
                        var errorResponse = r.ReadToEnd();
                        var status = GetValueFromJson(errorResponse, "//status");
                        SetStatusMessage(!string.IsNullOrEmpty(status) ? status : $@"Upload error '{errorResponse}'");
                    }

                    return;
                }

                var outputUrl = GetValueFromJson(responseString, "//output_url");

                if (string.IsNullOrEmpty(outputUrl))
                {
                    SetStatusMessage($@"Response error '{responseString}'");
                    return;
                }

                SetStatusMessage($@"Uploaded image '{inputImagePath}'");

                client.DownloadFileCompleted += DownloadCompletedHandler;
                client.DownloadFileAsync(new Uri(outputUrl), outputImagePath, outputImagePath);
                
            }
        }

        protected void DownloadCompletedHandler(object sender, AsyncCompletedEventArgs e)
        {
            var file = e.UserState.ToString();
            var fileName = Path.GetFileName(file);

            try
            {
                SetPreviewImage(file);
                SetStatusMessage($@"Downloaded image '{fileName}'");
            }
            catch (Exception)
            {
                SetStatusMessage($@"Could not preview downloaded image '{fileName}'");
            }
            
            SetProgressBar(progressBar.Value+1);

            if (progressBar.Value == progressBar.Maximum)
            {
                SetStatusMessage("Done!");
            }
        }

        public void SetProgressBar(int value, int? min = null, int? max = null)
        {
            progressBar.Invoke(new Action(() =>
            {
                if (min != null) progressBar.Minimum = min.Value;
                if (max != null) progressBar.Maximum = max.Value;
                progressBar.Value = value;
            }));
        }

        public void SetEditMode(bool enabled)
        {
            panel.Invoke(new Action(() =>
            {
                panel.Enabled = enabled;
            }));
        }

        public void SetStatusMessage(string message)
        {
            statusLabel.Invoke(new Action(() =>
            {
                statusLabel.Text = message;
            }));
        }

        public void SetPreviewImage(string imagePath)
        {
            Image image;
            using (var bmpTemp = new Bitmap(imagePath))
            {
                image = new Bitmap(bmpTemp);
            }

            previewPicture.Invoke(new Action(() =>
            {
                previewPicture.Image?.Dispose();
                previewPicture.Image = image;
            }));
        }

        protected void sourceButton_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                sourceTextBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        protected void destinationButton_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                destinationTextBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        protected void runButton_Click(object sender, EventArgs e)
        {
            var thread = Task.Factory.StartNew(() =>
            {
                SetEditMode(false);
                SetStatusMessage("");
                ProcessFolder(sourceTextBox.Text, destinationTextBox.Text);
                SetEditMode(true);
            });
        }

        protected void linkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://deepai.org/machine-learning-model/colorizer");
        }

        protected void apiKeyTextBox_Leave(object sender, EventArgs e)
        {
            ApiKey = apiKeyTextBox.Text;
        }

        protected void statusLabel_DoubleClick(object sender, EventArgs e)
        {
            Clipboard.SetText(statusLabel.Text);
        }

        public static string GetValueFromJson(string json, string path)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var jsonReader = JsonReaderWriterFactory.CreateJsonReader(
                Encoding.UTF8.GetBytes(json),
                new System.Xml.XmlDictionaryReaderQuotas());
            var root = XElement.Load(jsonReader);
            return root.XPathSelectElement(path)?.Value;
        }
    }
}
