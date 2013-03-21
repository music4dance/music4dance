using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

using DanceLibrary;
using System.Collections.ObjectModel;

namespace DanceCalc
{
    public class DanceViewModel : INotifyPropertyChanged
    {
        enum LoadState
        {
            Loading,
            Finished,
            Error
        }

        public DanceViewModel(DanceSample ds)
        {
            _ds = ds;
        }

        private DanceSample _ds;

        public DanceSample Dance { get { return _ds; } }

        public string Name { get { return _ds.DanceType.Name; } }

        public string Description
        {
            get
            {
                return _ds.DanceType.Description;
            }

            set
            {
                if (value != _ds.DanceType.Description)
                {
                    _ds.DanceType.Description = value;
                    NotifyPropertyChanged("Description");
                }
            }
        }

        public ReadOnlyCollection<DanceInstance> Instances
        {
            get { return _ds.Instances; }
        }

        public Uri Link
        {
            get
            {
                return _ds.DanceType.Link;
            }

            set
            {
                if (value != _ds.DanceType.Link)
                {
                    _ds.DanceType.Link = value;
                    NotifyPropertyChanged("Link");
                }
            }
        }

        public Visibility LoadVisible
        {
            get
            {
                return (_loadState == LoadState.Loading) ? Visibility.Visible : Visibility.Collapsed;
            }

            set
            {                
                Debug.Assert(value == Visibility.Visible);
                SetLoading(LoadState.Loading);
            }
        }

        public Visibility TextVisible
        {
            get
            {
                return (_loadState == LoadState.Finished) ? Visibility.Visible : Visibility.Collapsed;
            }

            set
            {
                Debug.Assert(value == Visibility.Visible);
                SetLoading(LoadState.Finished);
            }
        }

        public Visibility ErrorVisible
        {
            get
            {
                return (_loadState == LoadState.Error) ? Visibility.Visible : Visibility.Collapsed;
            }

            set
            {
                Debug.Assert(value == Visibility.Visible);
                SetLoading(LoadState.Error);
            }
        }

        private void SetLoading(LoadState ls)
        {
            if (ls == _loadState)
                return;

            _loadState = ls;

            NotifyPropertyChanged("LoadVisible");
            NotifyPropertyChanged("TextVisible");
            NotifyPropertyChanged("ErrorVisible");
        }

        private LoadState _loadState = LoadState.Loading;

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}