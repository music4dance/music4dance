using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Phone.Controls;

namespace DanceCalc
{
    public enum ToFrom { tfTo, tfFrom };

    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Set the data context of the listbox control to the sample data
            DataContext = App.ViewModel;
            this.Loaded += new RoutedEventHandler(MainPage_Loaded);

            //AdControl.TestMode = false;
            //ApplicationId="test_client" AdUnitId="Image480_80"
            //ApplicationId="74c95d69-3d58-40af-bbec-b807423cfdf0" AdUnitId="10018819"
            //Advertisement.ApplicationId = "74c95d69-3d58-40af-bbec-b807423cfdf0";
            //Advertisement.AdUnitId = "10018819";
        }

        // Handle selection changed on ListBox
        private void MainListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // If selected index is -1 (no selection) do nothing
            if (MainListBox.SelectedIndex == -1)
                return;

            // Navigate to the new page
            NavigationService.Navigate(new Uri("/DetailsPage.xaml?selectedItem=" + MainListBox.SelectedIndex, UriKind.Relative));

            // Reset selected index to -1 (no selection)
            MainListBox.SelectedIndex = -1;
        }

        // Load data for the ViewModel Items
        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!App.ViewModel.IsDataLoaded)
            {
                App.ViewModel.LoadData();
            }
            App.ViewModel.UpdateDances();

            if (App.ViewModel.Counting)
            {
                if (_timer != null)
                {
                    _timer.Stop();
                }
                App.ViewModel.UserPaused();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            //this.viewModel.SyncStateToAppState();
            //ApplicationState.AddAppObjects();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);           
        }

        private void Count_Click(object sender, RoutedEventArgs e)
        {
            App.ViewModel.Count();
            ResetTimer();
        }

        private void ResetTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(10);
                _timer.Tick += new EventHandler(UserPaused);
            }

            _timer.Start();
        }

        private void UserPaused(object sender, EventArgs e)
        {
            App.ViewModel.UserPaused();

            _timer.Stop();
        }

        private DispatcherTimer _timer;

        private void Swap_Click(object sender, RoutedEventArgs e)
        {

        }

        private void FromMenu_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            NavigationService.Navigate(new Uri("/TypeChooser.xaml?page=From", UriKind.RelativeOrAbsolute));
        }

        private void ToMenu_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ChangeTo();
        }

        private void To_Click(object sender, EventArgs e)
        {
            ChangeTo();
        }

        private void ChangeTo()
        {
            NavigationService.Navigate(new Uri("/TypeChooser.xaml?page=To", UriKind.RelativeOrAbsolute));
        }

        private void Length_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ChangeLength();
        }

        private void Length_Click(object sender, EventArgs e)
        {
            ChangeLength();
        }

        private void ChangeLength()
        {
            NavigationService.Navigate(new Uri("/LengthChooser.xaml", UriKind.RelativeOrAbsolute));
        }

        private void Filter_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Filters.xaml", UriKind.RelativeOrAbsolute));
        }

        private void Clear_Click(object sender, EventArgs e)
        {
            if (_timer != null)
                _timer.Stop();

            App.ViewModel.Clear();
        }

        private void Help_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Help.xaml", UriKind.RelativeOrAbsolute));
        }

    }
}