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
    /*
     * This is a bit twisted as I started out wanting to have symetrical 'To' and 'From' fields on the main page that
     * could contain any of the meters or duration types.  For now in the interest of reducing feature creap, I'm going
     * to keep the From pinned to meter or beat per minute and the to as a duration.  But I'm going to try to leave
     * the bulk of the generic to/from code in place so that I can build on it later
     */

    public partial class TypeChooser : PhoneApplicationPage
    {
        public TypeChooser()
        {
            InitializeComponent();

            //this.Loaded += new RoutedEventHandler(MainPage_Loaded);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _isLoading = true;

            try
            {
                string pageName;
                bool itemExists = NavigationContext.QueryString.TryGetValue("page", out pageName);
                if (itemExists)
                {
                    _isTo = pageName == "To";

                    if (_isTo)
                        DataContext = Conversands.Durations;
                    else
                        DataContext = Conversands.Meters;

                    PageTitle.Text = pageName + " Settings";

                    // Set the selection to the current item

                    foreach (object item in Chooser.Items)
                    {
                        IConversand conversand = item as IConversand;
                        if (conversand.Equals(Current))
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

        private IConversand Current        
        {
            get
            {
                if (_isTo)
                {
                    return App.ViewModel.To;
                }
                else
                {
                    return App.ViewModel.From;
                }
            }

            set
            {
                if (_isTo)
                {
                    App.ViewModel.To = value;
                }
                else
                {
                    App.ViewModel.From = value;
                }
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
                IConversand c = Chooser.SelectedItem as IConversand;
                if (c != null)
                {
                    Current = c;
                }
                NavigationService.GoBack();
            }
        }

        private bool _isTo;
        private bool _isLoading;
    }
}