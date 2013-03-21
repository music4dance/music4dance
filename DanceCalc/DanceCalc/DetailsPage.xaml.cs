using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;

using DanceLibrary;
using System.IO;
using System.Xml.Linq;
using System.Xml;
using Microsoft.Phone.Tasks;
using Microsoft.Advertising.Mobile.UI;

namespace DanceCalc
{
    public partial class DetailsPage : PhoneApplicationPage
    {

        // Constructor
        public DetailsPage()
        {
            InitializeComponent();

            //AdControl.TestMode = false;
        }

        // When page is navigated to set data context to selected item in list
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            string selectedIndex = "";
            if (NavigationContext.QueryString.TryGetValue("selectedItem", out selectedIndex))
            {
                int index = int.Parse(selectedIndex);
                if (index < App.ViewModel.Dances.Count)
                {
                    DanceSample ds = App.ViewModel.Dances[index];
                    _dvm = new DanceViewModel(ds);
                    DataContext = _dvm;
                    Debug.WriteLine(string.Format("Details page DataContext={0}",_dvm.Name));

                    if (ds.DanceType.Description != null)
                    {
                        _dvm.TextVisible = Visibility.Visible;
                    }
                    else
                    {
                        Load();
                    }
                }
            }
        }

        private void More_Click(object sender, RoutedEventArgs e)
        {
            // Let's be more defensive here...
            if (_dvm == null || _dvm.Link == null)
                return;

            string s = _dvm.Link.ToString();
            if (!string.IsNullOrEmpty(s))
            {
                try
                {
                    WebBrowserTask task = new WebBrowserTask();
                    if (task != null)
                    {
                        task.URL = s;
                        task.Show();
                    }
                }
                catch (System.InvalidOperationException ex)
                {
                    Debugger.Log(1, "DetailsPage", ex.Message);
                }
            }
        }
        

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            StartDownload();
        }


        private WebClient WebClient
        {
            get
            {
                if (_webClient == null)
                {
                    _webClient = new WebClient();
                    _webClient.DownloadStringCompleted += new DownloadStringCompletedEventHandler(dc_DownloadStringCompleted);
                }
                return _webClient;
            }
        }

        void dc_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            string s = null;
            string name = null;

            if (!e.Cancelled && e.Error == null)
            {
                s = e.Result as string;
                name = e.UserState as string;
            }

            if (s != null && name != null)
            {
                using (TextWriter w = TempStorage.Instance.GetTextWriter(name + ".xml"))
                {
                    w.Write(s);
                }

                Load();
            }
        }

        private void StartDownload()
        {
            _dvm.LoadVisible = Visibility.Visible;

            WebClient wc = WebClient;

            while (wc.IsBusy)
            {
                wc.CancelAsync();
            }

            string name = _dvm.Dance.DanceType.ShortName;
            Uri uri = new Uri("http://www.thegray.com/dances/" + name + ".xml");
            wc.DownloadStringAsync(uri, name);
        }

        private void Load()
        {
            string name = _dvm.Dance.DanceType.ShortName;

            using (TextReader t = TempStorage.Instance.GetTextReader(name + ".xml"))
            {
                if (t == null)
                {
                    StartDownload();
                }
                else
                {
                    try
                    {
                        XDocument doc = XDocument.Load(t);
                        XElement dance = doc.Element("Dance");

                        XElement l = dance.Element("Link");
                        if (l != null)
                        {
                            _dvm.Link = new Uri(l.Value);
                        }

                        XElement s = dance.Element("Summary");
                        if (s != null)
                        {
                            _dvm.Description = s.Value;
                            _dvm.TextVisible = Visibility.Visible;
                        }
                    }
                    catch (XmlException e)
                    {
                        Debugger.Log(1, "DetailsPage", e.Message);
                        _dvm.ErrorVisible = Visibility.Visible;
                    }
                    catch (Exception e)
                    {
                        Debugger.Log(1, "DetailsPage", e.Message);
                        _dvm.ErrorVisible = Visibility.Visible;
                    }
                }
            }
        }

        private static WebClient _webClient;
        private static DanceViewModel _dvm;
    }
}