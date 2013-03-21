using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;

using DanceLibrary;

namespace DanceCalc
{
    public partial class LengthChooser : PhoneApplicationPage
    {
        public LengthChooser()
        {
            InitializeComponent();

            DataContext = SongDuration.GetStandarDurations();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _isLoading = true;

            try
            {
                SongDuration c = Current;
                foreach (object item in Chooser.Items)
                {
                    if (item is SongDuration)
                    {
                        SongDuration sd = (SongDuration)item;
                        if (sd.Equals(c))
                        {
                            Chooser.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private SongDuration Current
        {
            get
            {
                return App.ViewModel.Length;
            }

            set
            {
                App.ViewModel.Length = value;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void PhoneApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void Chooser_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoading)
            {
                if (Chooser.SelectedItem is SongDuration)
                {
                    Current = (SongDuration)Chooser.SelectedItem;
                }
                NavigationService.GoBack();
            }
        }

        bool _isLoading;
    }
}