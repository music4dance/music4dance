using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Threading;

namespace SongDatabase.Models
{
    public class DbObject
    {
        public DbObject()
        {
            InstanceId = Interlocked.Increment(ref _instanceCount);
        }

        [NotMapped]
        public int InstanceId { get; private set; }

        public virtual void Dump()
        {
            Debug.Write(string.Format("0X{0:X4}: ",InstanceId));
        }

        static public int InstanceCount
        {
            get { return _instanceCount; }
        }

        static int _instanceCount = 0;
    }

    public class Song : DbObject
    {
        public int SongId { get; set; }
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Publisher { get; set; }
        public string Genre { get; set; }
        public int Track { get; set; }
        public int Length { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public int TitleHash { get; set; }
        public string Purchase { get; set; }
        public virtual ICollection<Dance> Dances { get; set; }
        public virtual ICollection<UserProfile> ModifiedBy { get; set; }
        public virtual ICollection<SongProperty> SongProperties { get; set; }

        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},Title={1},Album={2},Artist={3}",SongId,Title,Album,Artist);
            Debug.WriteLine(output);
            if (ModifiedBy != null)
            {
                foreach (UserProfile user in ModifiedBy)
                {
                    Debug.Write("\t");
                    user.Dump();
                }
            }
        }
    }

    public class SongProperty : DbObject
    {
        public Int64 Id { get; set; }
        public int SongId { get; set; }
        public virtual Song Song { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},SongId={1},Name={2},Value={3}",Id, SongId,Name,Value);
            Debug.WriteLine(output);
        }
    }
}