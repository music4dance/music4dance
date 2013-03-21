using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DanceLibrary
{
    public class Tempo
    {
        public Tempo(DanceObject d, string name)
        {
            decimal t = 0;
            _minTempo = 0;
            _maxTempo = 0;

            if (d.TryGetDecimalAttribute(name, out t))
            {
                _maxTempo = t;
                _minTempo = t;
            }
            else if (d.TryGetDecimalAttribute("Min" + name, out t))
            {
                decimal t2 = 0;
                if (d.TryGetDecimalAttribute("Max" + name, out t2))
                {
                    _minTempo = t;
                    _maxTempo = t2;
                }
            }
        }

        public Tempo(Tempo other)
        {
            _minTempo = other._minTempo;
            _maxTempo = other._maxTempo;
        }

        public Tempo(decimal minTempo, decimal maxTempo)
        {
            _minTempo = minTempo;
            _maxTempo = maxTempo;
        }

        public bool IsValid
        {
            get {return _maxTempo != 0M;}
        }

        public decimal Min
        {
            get { return _minTempo; }
        }

        public decimal Max
        {
            get { return _maxTempo; }
        }

        public decimal Average
        {
            get { return _minTempo + (_maxTempo - _minTempo) / 2; }
        }

        public string MinString
        {
            get { return Format(_minTempo); }
        }

        public string MaxString
        {
            get { return Format(_maxTempo); }
        }

        public string AverageString
        {
            get { return Format(Average); }
        }

        public decimal CalculateDelta(decimal tempo)
        {
            decimal delta = 0;
            if (tempo > Max)
            {
                delta = tempo - Max;
            }
            else if (tempo < Min)
            {
                delta = tempo - Min;
            }
            return delta;
        }

        public Tempo Include(Tempo other)
        {
            if (other == null)
                return new Tempo(this);
            else
                return new Tempo(Math.Min(_minTempo, other._minTempo), Math.Max(_maxTempo, other._maxTempo));
        }

        public override string ToString()
        {
            if (_minTempo == _maxTempo)
                return MinString;
            else
                return string.Format("{0}-{1}", MinString, MaxString);
        }

        private string Format(decimal d)
        {
            decimal i = Math.Round(d);
            if (Math.Abs(i - d) < .01M)
            {
                return i.ToString("F0");
            }
            else
            {
                return d.ToString("F2");
            }
        }

        decimal _minTempo;
        decimal _maxTempo;
    }
}
