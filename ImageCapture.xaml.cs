using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Capture;
using Windows.Storage;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Foundation.Metadata;
using Windows.System.Profile;
using System.Diagnostics;
using Windows.Media.MediaProperties;
using Windows.Media.SpeechSynthesis;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace App1
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ImageCapture : Page
    {
        MediaCapture _mediaCapture;
        bool _isPreviewing;
        bool _periodicEnabled;
        DispatcherTimer _timer;

        public ImageCapture()
        {
            this.InitializeComponent();
            _mediaCapture = new MediaCapture();
            _mediaCapture.InitializeAsync();
            capturePreview.Source = null;
            _isPreviewing = false;
            _periodicEnabled = false;
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Tick += _timer_Tick;
        }

        private async void _timer_Tick(object sender, object e)
        {
            var t = await Task.Factory.StartNew(() => Capture());
            var json = await t;
            result.Text = json;
            await Say(result.Text);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            this.result.Text = "Analyzing... please wait";
            await Say(result.Text);
            var t = await Task.Factory.StartNew(()=> Capture()   );
            var json = await t;
            result.Text = json;
            await Say(result.Text);
        }

        async Task Say(string text)
        {
            IRandomAccessStream stream = await this.SynthesizeTextToSpeechAsync(text);

            await mediaElement.PlayStreamAsync(stream, true);
        }
        

        private async Task<string> Capture()
        {
            
            var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
            // Fall back to the local app storage if the Pictures Library is not available
            var _captureFolder = picturesLibrary.SaveFolder ?? ApplicationData.Current.LocalFolder;

            var file = await _captureFolder.CreateFileAsync("SimplePhoto.jpg", CreationCollisionOption.GenerateUniqueName);
            await _mediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);
            
            Debug.WriteLine("Photo taken! Saving to " + file.Path);

            var json = await ComputerVisionHelper.MakeAnalysisRequest(file.Path);
            return json;
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (capturePreview.Source == null)
            {
                capturePreview.Source = _mediaCapture;
            }
            if (!_isPreviewing)
            {
                await _mediaCapture.StartPreviewAsync();
            }
            else
            {
                await _mediaCapture.StopPreviewAsync();
            }
            _isPreviewing = !_isPreviewing;
        }
        async Task<IRandomAccessStream> SynthesizeTextToSpeechAsync(string text)
        {
            // Windows.Storage.Streams.IRandomAccessStream
            IRandomAccessStream stream = null;

            // Windows.Media.SpeechSynthesis.SpeechSynthesizer
            using (SpeechSynthesizer synthesizer = new SpeechSynthesizer())
            {
                // Windows.Media.SpeechSynthesis.SpeechSynthesisStream
                stream = await synthesizer.SynthesizeTextToStreamAsync(text);
            }

            return (stream);
        }

        private void PeriodocBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_periodicEnabled)
            {
                PeriodocBtn.Content = "Start Periodic";
                _timer.Stop();
            }
            else
            {
                PeriodocBtn.Content = "Stop Periodic";
                this._timer.Start();
            }
            _periodicEnabled = !_periodicEnabled;
            
        }
    }
}
static class MediaElementExtensions
{
    public static async Task PlayStreamAsync(
      this MediaElement mediaElement,
      IRandomAccessStream stream,
      bool disposeStream = true)
    {
        // bool is irrelevant here, just using this to flag task completion.
        TaskCompletionSource<bool> taskCompleted = new TaskCompletionSource<bool>();

        // Note that the MediaElement needs to be in the UI tree for events
        // like MediaEnded to fire.
        RoutedEventHandler endOfPlayHandler = (s, e) =>
        {
            if (disposeStream)
            {
                stream.Dispose();
            }
            taskCompleted.SetResult(true);
        };
        mediaElement.MediaEnded += endOfPlayHandler;

        mediaElement.SetSource(stream, string.Empty);
        mediaElement.Play();

        await taskCompleted.Task;
        mediaElement.MediaEnded -= endOfPlayHandler;
    }
}