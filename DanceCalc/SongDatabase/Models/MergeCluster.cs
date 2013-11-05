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
        public MergeCluster(int hash)
        {
            PropertyHash = hash;
            Songs = new List<Song>();
        }

        public int PropertyHash { get; set; }
        public List<Song> Songs { get; set; }

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
                    Dictionary<int, MergeCluster> lumps = new Dictionary<int, MergeCluster>();

                    bool emptyArtist = false;
                    foreach (Song s in cluster.Songs)
                    {
                        if (string.IsNullOrWhiteSpace(s.Artist))
                        {
                            emptyArtist = true;
                            break;
                        }

                        MergeCluster lump;
                        int hash = DanceMusicContext.CreateTitleHash(s.Artist);
                        if (!lumps.TryGetValue(hash, out lump))
                        {
                            
                            lump = new MergeCluster(hash);
                            lumps.Add(hash, lump);
                        }

                        lump.Songs.Add(s);
                    }

                    if (emptyArtist)
                    {
                        // Add all of the songs in the cluster
                        ret.AddRange(cluster.Songs);
                    }
                    else
                    {
                        foreach (MergeCluster l in lumps.Values)
                        {
                            if (ret.Count + l.Songs.Count > n)
                            { 
                                break; 
                            }

                            if (l.Songs.Count > 1)
                            {
                                ret.AddRange(l.Songs);
                            }
                        }
                    }
                }
            }

            return ret;
        }
    }
}
