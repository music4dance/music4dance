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
using System.Windows.Shapes;
using Microsoft.Phone.Controls;

using DanceLibrary;
using Microsoft.Phone.Tasks;

namespace DanceCalc
{
    public partial class Filters : PhoneApplicationPage
    {
        public Filters()
        {
            InitializeComponent();

            _choosers = new Dictionary<ListBox,string>();
            _choosers.Add(StyleChooser,"Style");
            _choosers.Add(OrganizationChooser, "Organization");
            _choosers.Add(LevelChooser, "Level");
            _choosers.Add(CompetitorChooser, "Competitor");

            foreach (ListBox chooser in _choosers.Keys)
            {
                SetList(chooser, _choosers[chooser]);
            }

            DataContext = App.ViewModel;
        }

        private void SetList(ListBox lb, string name)
        {
            foreach (FilterItem fi in FilterObject.GetFilter(name))
            {
                ListBoxItem lbi = new ListBoxItem();
                lbi.Tag = fi;
                lbi.Content = fi.LongName;
                lbi.IsSelected = fi.Value;
                lb.Items.Add(lbi);
            }
        }

        private void Chooser_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox lb = sender as ListBox;

            foreach (ListBoxItem lbi in e.AddedItems)
            {
                FilterItem fi = lbi.Tag as FilterItem;
                if (fi.Value != true)
                    fi.Value = true;
            }

            foreach (ListBoxItem lbi in e.RemovedItems)
            {
                FilterItem fi = lbi.Tag as FilterItem;
                if (fi.Value != false)
                    fi.Value = false;
            }
        }

        Dictionary<ListBox,string> _choosers;

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            App.ViewModel.Reset();
            FilterObject.SetAll(true);
        }

        private void Feedback_Click(object sender, RoutedEventArgs e)
        {
            EmailComposeTask task = new EmailComposeTask();
            task.To = "dc@theGray.com";
            task.Subject = "Feedback on Dance Calculater WP7 application";

            task.Show();
        }
    }
}