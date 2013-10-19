using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongDatabase.Models
{
    class MergeCluster
    {
        public MergeCluster(int titleHash)
        {
            TitleHash = titleHash;
            Songs = new List<Song>();
        }

        public int TitleHash { get; set; }
        public List<Song> Songs { get; set; }

        public static int HashSong(Song song)
        {
            int ret = song.TitleHash;

            // TODO: Should really have a more specific normalization function for artist (first, last)
            ret = ret ^ DanceMusicContext.CreateTitleHash(song.Artist);

            return ret;
        }

        public static List<Song> GetMergeCandidates(DanceMusicContext dmc, int n)
        {
            var songs = from s in dmc.Songs select s;

            Dictionary<int,MergeCluster> clusters = new Dictionary<int,MergeCluster>();


            foreach (Song song in dmc.Songs)
            {
                //Debug.WriteLine("{0}\t{1}", song.TitleHash, song.Title);

                MergeCluster mc = null;
                if (!clusters.TryGetValue(song.TitleHash, out mc))
                {
                    mc = new MergeCluster(song.TitleHash);
                    clusters.Add(song.TitleHash, mc);
                }

                mc.Songs.Add(song);
            }

            // Consider improving this algorithm, but for now, just take the top n songs
            List<Song> ret = new List<Song>();

            foreach (MergeCluster cluster in clusters.Values)
            {
                if (ret.Count + cluster.Songs.Count > n)
                {
                    break;
                }

                if (cluster.Songs.Count > 1)
                {
                    ret.AddRange(cluster.Songs);
                }
            }

            return ret;
        }
    }
}
