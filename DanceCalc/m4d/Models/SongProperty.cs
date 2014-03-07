using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Threading;


namespace m4d.Models
{
    public class SongProperty : DbObject
    {
        // Name Syntax: [+-]BaseName[:idx[:qual]]
        // Where default is repalce, + is add, - is delete
        // idx is zeros based indes for multi-value fields (only album at this point?)
        // qual is a qualifier for purchase type (may generalize?)
        //
        // Not implementing this yet, but for artist might allow artist type after the colon
        
        public SongProperty()
        {
        }

        public SongProperty(int songId, string name, string value)
        {
            SongId = songId;
            Name = name;
            Value = value;
        }
        public SongProperty(int songId, string baseName, string value=null, int index = -1, string qual = null)
        {
            SongId = songId;

            string name = null;

            if (index >= 0)
            {
                name = string.Format("{0}:{1:2d}",name,index);
            }

            if (qual != null)
            {
                name = name + ":" + qual;
            }

            Name = name;
            Value = value;
        }

        public Int64 Id { get; set; }
        public int SongId { get; set; }
        public virtual Song Song { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public object ObjectValue
        {
            get
            {
                object ret = null;
                switch (BaseName)
                {
                    case DanceMusicContext.TempoField:
                        // decimal
                        if (Value != null)
                        {
                            decimal v;
                            decimal.TryParse(Value, out v);
                            ret = v;
                        }
                        break;
                    case DanceMusicContext.LengthField:
                    case DanceMusicContext.TrackField:
                    case DanceMusicContext.DanceRatingField:
                        //int
                        if (Value != null)
                        {
                            int v;
                            int.TryParse(Value, out v);
                            ret = v;
                        }
                        break;
                    case DanceMusicContext.TimeField:
                        // time
                        break;

                    default:
                        ret = Value;
                        break;
                }

                return ret;
            }
        }
        public bool IsComplex 
        {
            get { return IsComplexName(Name); }
        }
        public bool IsAction
        {
            get { return IsActionName(Name); }
        }

        public static bool IsComplexName(string name) 
        { 
            return name.Contains(":");
        }
        public static bool IsActionName(string name)
        {
            return name.StartsWith(".");
        }

        public string BaseName
        {
            get
            {
                string baseName = Name;

                // TODO: Deprecate +/-
                if (baseName.StartsWith("+")|| baseName.StartsWith("-"))
                {
                    baseName = baseName.Substring(1);
                }

                int i = baseName.IndexOf(':');

                if (i >= 0)
                {
                    baseName = baseName.Substring(0, i);
                }

                return baseName;
            }
        }

        public int Index
        {
            get
            {
                int idx = 0;

                if (Name.Contains(":"))
                {
                    string[] parts = Name.Split(new char[] { ':' });

                    if (parts.Length > 1)
                    {
                        int.TryParse(parts[1], out idx);
                    }
                }                

                return idx;
            }
        }

        public string Qualifier
        {
            get
            {
                string qual = null;

                if (Name.Contains(":"))
                {
                    string[] parts = Name.Split(new char[] { ':' });

                    if (parts.Length > 2)
                    {
                        qual = parts[2];
                    }
                }

                return qual;
            }
        }

        public static string FormatName(string baseName, int? idx = null, string qualifier = null)
        {
            string name = baseName;

            if (idx != null)
            {
                name += ":" + idx.ToString();
            }

            if (qualifier != null)
            {
                name += ":" + qualifier;
            }

            return name;
        }

        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},SongId={1},Name={2},Value={3}", Id, SongId, Name, Value);
            Debug.WriteLine(output);
        }
    }
}
