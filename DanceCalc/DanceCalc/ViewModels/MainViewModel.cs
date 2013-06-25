using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using DanceLibrary;

namespace DanceCalc
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public MainViewModel()
        {
            this.Dances = new ObservableCollection<DanceSample>();
        }

        private MainPageState PageState
        {
            get { return ApplicationState.MainPageInfo; }
        }


        public IConversand From
        {
            get { return PageState.From; }

            set
            {
                if (value != PageState.From)
                {
                    PageState.From = value;
                    SetAType(value);
                    NotifyPropertyChanged("From");
                    NotifyPropertyChanged("FromValue");
                    UpdateDances();
                }
            }
        }

        public IConversand To
        {
            get { return PageState.To; }

            set
            {
                if (value != PageState.To)
                {
                    PageState.To = value;
                    if (PageState.From.Kind != value.Kind)
                        SetAType(value);

                    NotifyPropertyChanged("To");
                    NotifyPropertyChanged("ToValue");
                }
            }
        }

        public decimal FromValue
        {
            get 
            { 
                return GetAValue(PageState.From); 
            }

            set
            {
                if (SetAValue(value,PageState.From))
                {
                    NotifyPropertyChanged("From");
                    NotifyPropertyChanged("FromValue");
                    NotifyPropertyChanged("ToValue");
                }
            }
        }

        public decimal ToValue
        {
            get 
            { 
                return GetAValue(PageState.To); 
            }

            set
            {
                if (SetAValue(value, PageState.To))
                {
                    NotifyPropertyChanged("To");
                    NotifyPropertyChanged("ToValue");
                }
            }
        }

        public SongDuration Length
        {
            get
            {
                return PageState.Timing.Duration;
            }

            set
            {
                if (PageState.Timing.Duration.Length != value)
                {
                    PageState.Timing.Duration = new SongDuration(value);
                    NotifyPropertyChanged("Length");
                    NotifyPropertyChanged("FromValue");
                    NotifyPropertyChanged("ToValue");
                }
            }
        }

        public decimal Epsilon
        {
            get
            {
                return PageState.Epsilon;
            }

            set
            {
                if (PageState.Epsilon != value)
                {
                    PageState.Epsilon = value;
                    NotifyPropertyChanged("Epsilon");
                }
            }
        }

        public bool Counting
        {
            get
            {
                return PageState.Counting;
            }

            set
            {
                if (PageState.Counting != value)
                {
                    PageState.Counting = value;
                    NotifyPropertyChanged("Counting");
                }
            }

        }

        public void Count()
        {
            _st.DoClick();
            Decimal r = _st.Rate;
            if (Counting == true)
            {
                PageState.Timing.SetRate(r);

                UpdateDances();

                NotifyPropertyChanged("FromValue");
                NotifyPropertyChanged("ToValue");
            }
            else
            {
                Counting = true;
            }
        }

        public void UserPaused()
        {
            Counting = false;
            _st.Reset();
        }

        public void Clear(bool includeTimer = true)
        {
            PageState.Reset();
            NotifyPropertyChanged("From");
            NotifyPropertyChanged("FromValue");
            NotifyPropertyChanged("To");
            NotifyPropertyChanged("ToValue");
            NotifyPropertyChanged("Length");
            NotifyPropertyChanged("Epsilon");
            NotifyPropertyChanged("Counting");
            UpdateDances();

            if (includeTimer)
            {
                _st = new SongTimer();
            }
        }

        public void Reset()
        {
            Clear();            
            TempStorage.Instance.CleanStorage();
        }

        private void SetAType(IConversand conversand)
        {
            switch (conversand.Kind)
            {
                case Kind.Tempo:
                    PageState.Timing.Convert(conversand as TempoType);
                    break;
                case Kind.Duration:
                    PageState.Timing.Convert(conversand as DurationType);
                    break;                
            }
        }

        private decimal GetAValue(IConversand conversand)
        {
            decimal value = -1;
            switch (conversand.Kind)
            {
                case Kind.Tempo:
                    value = PageState.Timing.Tempo.Rate;
                    break;
                case Kind.Duration:
                    value = PageState.Timing.GetBiasedLength();
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }

            return Math.Round(value,2);
        }

        private bool SetAValue(decimal value, IConversand conversand)
        {
            if (GetAValue(conversand) == value)
                return false;

            switch (conversand.Kind)
            {
                case Kind.Tempo:
                    PageState.Timing.SetDenormalizedRate(value);
                    UpdateDances();
                    break;
                case Kind.Duration:
                    PageState.Timing.SetLength(value);
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }

            return true;
        }

        /// <summary>
        /// A collection for DanceViewModel objects.
        /// </summary>
        public ObservableCollection<DanceSample> Dances { get; private set; }

        public bool IsDataLoaded
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates and adds a the currently filtered dances to the collection
        /// </summary>
        public void LoadData()
        {
            Debug.Assert(this.IsDataLoaded == false);
            Debug.Assert(_dances == null);

            _dances = new Dances();

            UpdateDances();

            this.IsDataLoaded = true;
        }

        public void UpdateDances()
        {
            Dances.Clear();

            IEnumerable<DanceSample> d = _dances.DancesFiltered(PageState.Timing.Tempo, PageState.Epsilon);
            foreach (DanceSample el in d)
            {
                Dances.Add(el);
            }
        }

        private Dances _dances;

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private SongTimer _st = new SongTimer();
    }
}