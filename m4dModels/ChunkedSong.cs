using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using DanceLibrary;

namespace m4dModels
{
    public class SongChunk(int id, IEnumerable<SongProperty> properties)
    {
        public int Id { get; } = id;
        public string User => Properties.FirstOrDefault(x => x.BaseName == Song.UserField)?.Value;
        public List<SongProperty> Properties { get; } = [.. properties];

        public SongProperty MatchRating(string rating)
        {
            var dance = Dances.Instance.DanceFromId(rating[..3]) ?? throw new Exception($"Dance {rating} not found");
            return Properties.FirstOrDefault(
                x => x.BaseName == Song.AddedTags && x.Value.Contains($"{dance.Name}:Dance"));
        }
        public IList<SongProperty> MatchTags(string tagList)
        {
            var tags = new TagList(tagList).Filter("Dance").Strip();
            var ids = tags.Select(Dances.Instance.DanceFromName).Where(d => d != null).Select(d => d.Id).ToList();
            return [.. Properties.Where(
                x => x.BaseName == Song.DanceRatingField && ids.Any(t => x.Value.StartsWith(t)))];
        }

        public bool HasGroupTag(string rating)
        {
            var dance = Dances.Instance.DanceFromId(rating[..3]) ?? throw new Exception($"Dance {rating} not found");
            var group = (dance is DanceType t ? t.Groups.FirstOrDefault() : null) ?? throw new Exception($"Group not for for {rating}");
            if (!GroupIsTight(group.Id))
            {
                return false;
            }

            return Properties.Any(x => x.BaseName == Song.AddedTags && group.Members.Any(d => x.Value.Contains($"{d.Name}:Dance")));
        }

        public bool CheckHeader()
        {
            return Properties.Count >= 3 && Properties[0].IsAction && ((Properties[1].BaseName == Song.UserField && Properties[2].BaseName == Song.TimeField) || (Properties[2].BaseName == Song.UserField && Properties[1].BaseName == Song.TimeField));
        }
        public static bool GroupIsTight(string id)
        {
            return id == "SWG" || id == "FXT" || id == "TNG" || id == "WLZ";
        }
    }

    public class ChunkedSong
    {
        public List<SongChunk> Chunks { get; } = [];
        public Dictionary<string, List<SongChunk>> UserChunks { get; } = [];
        public string SongId { get; } = "TESTING";

        public ChunkedSong(string song) => Chunk(SongProperty.Load(song));

        public ChunkedSong(Song song)
        {
            SongId = song.SongId.ToString();
            Chunk(song.SongProperties);
        }

        public List<SongProperty> SongProperties => [.. Chunks.SelectMany(x => x.Properties)];

        public string Serialize()
        {
            return string.Join("\t", SongProperties);
        }

        public bool HasInvalidBatch()
        {
            foreach (var chunk in GetBatchChunks())
            {
                foreach (var property in chunk.Properties)
                {
                    if (property.BaseName == Song.DanceRatingField)
                    //|| (property.BaseName == Song.AddedTags && property.Value.Contains(":Dance")))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool RemoveInvalidBatches()
        {
            // We're allowing for generalized derivites (SWG, FXT, TNG, WLZ)
            var changed = false;
            foreach (var chunk in GetBatchChunks())
            {
                var badProps = new List<SongProperty>();
                foreach (var property in chunk.Properties)
                {
                    if (property.BaseName == Song.DanceRatingField && IsSwingy(property.Value[..3]))
                    {
                        badProps.Add(property);
                    }
                    else if (chunk.User == "batch|P" && property.BaseName == Song.DanceRatingField)
                    {
                        badProps.Add(property);
                    }
                    //else if (chunk.User == "batch|P" && property.BaseName == Song.AddedTags && property.Value.Contains(":Dance"))
                    //{
                    //    Trace.WriteLine($"{SongId}: Removing {property}");
                    //    badProps.Add(property);
                    //}
                }
                foreach (var badProp in badProps)
                {
                    chunk.Properties.Remove(badProp);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsSwingy(string value)
        {
            var dance = Dances.Instance.DanceFromId(value) ?? throw new Exception($"Dance {value} not found");
            if (dance is DanceType t)
            {
                dance = t.Groups.FirstOrDefault();
            }

            return SongChunk.GroupIsTight(dance.Id);
        }

        public bool HasOrphanedVote()
        {
            foreach (var chunk in Chunks.Where(x => !IsBatch(x.User)))
            {
                if (chunk.Properties.Any(x => x.BaseName == Song.DanceRatingField) &&
                    !chunk.Properties.Any(x => x.BaseName == Song.AddedTags && x.Value.Contains(":Dance")))
                {
                    return true;
                }
            }

            return false;
        }

        public bool DedupRatings()
        {
            var changed = false;
            foreach (var pair in UserChunks.Where(p => IsBatch(p.Key) || IsPsuedoUser(p.Key)))
            {
                var map = new HashSet<string>();
                foreach (var chunk in pair.Value)
                {
                    var badRating = false;
                    var remove = new List<SongProperty>();
                    foreach (var property in chunk.Properties)
                    {
                        if (property.BaseName == Song.DanceRatingField)
                        {
                            if (map.Contains(property.Value))
                            {
                                remove.Add(property);
                            }
                            else
                            {
                                if (property.Value.Contains('-'))
                                {
                                    Trace.WriteLine($"{SongId}: Negative rating on {SongId}, {chunk.User}: {property.Value}");
                                    badRating = true;
                                    break;
                                }
                                map.Add(property.Value);
                            }
                        }
                    }
                    if (badRating)
                    {
                        break;
                    }
                    foreach (var property in remove)
                    {
                        chunk.Properties.Remove(property);
                        changed = true;
                    }
                }
            }
            return changed;
        }

        // This should bring tags forward that correspond to a user's votes
        public async Task<bool> CleanOrphanedVotes(DanceMusicCoreService dms, bool removeUnmatched = false, bool addUnmatched = false, bool onlyGroups = false)
        {
            var changed = false;
            var batchChunks = await FilterUnbalanced("batch|P", UserChunks.GetValueOrDefault("batch|P"), dms);

            //foreach (var pair in UserChunks.Where(p => p.Key.Equals("DWTS|P",StringComparison.OrdinalIgnoreCase)))
            foreach (var pair in UserChunks.Where(p => !IsBatch(p.Key)))
            {
                //if (await AreRatingsBalanced(pair.Key, pair.Value, dms)) {
                //    continue;
                //}

                var badChunks = await FilterUnbalanced(pair.Key, pair.Value, dms);

                var c = badChunks.Count;
                for (var i = 0; i < c; i++)
                {
                    var chunk = badChunks[i];
                    var badProps = new List<SongProperty>();
                    var added = new List<SongProperty>();
                    var remove = new List<SongProperty>();
                    foreach (var property in chunk.Properties)
                    {
                        if (property.BaseName == Song.DanceRatingField && chunk.MatchRating(property.Value) == null)
                        {
                            SongProperty replace = null;
                            for (var j = i + 1; j < c; j++)
                            {
                                var other = badChunks[j];
                                replace = other.MatchRating(property.Value);
                                if (replace != null)
                                {
                                    other.Properties.Remove(replace);
                                    break;
                                }
                                ;
                            }

                            if (replace == null && batchChunks != null)
                            {
                                // Search in batch chunks
                                foreach (var bc in batchChunks)
                                {
                                    replace = bc.MatchRating(property.Value);
                                    if (replace != null)
                                    {
                                        bc.Properties.Remove(replace);
                                        break;
                                    }
                                }
                            }

                            if (replace != null)
                            {
                                added.Add(replace);
                                changed = true;
                            }
                            else if (addUnmatched)
                            {
                                var dance = Dances.Instance.DanceFromId(property.Value[..3]) ?? throw new Exception($"Dance {property.Value} not found");
                                var name = dance.Name;
                                if (property.Value.EndsWith("-1"))
                                {
                                    name = $"!{name}";
                                }
                                added.Add(new SongProperty(Song.AddedTags, $"{name}:Dance"));
                                changed = true;
                            }
                            else
                            {
                                if (removeUnmatched && (!onlyGroups || chunk.HasGroupTag(property.Value)))
                                {
                                    remove.Add(property);
                                    changed = true;
                                }
                                Trace.WriteLine($"{SongId}: Couldn't find tag for {property.Value} on {chunk.User}");
                            }
                        }
                    }
                    chunk.Properties.AddRange(added);
                    added.Clear();
                    foreach (var p in remove)
                    {
                        chunk.Properties.Remove(p);
                    }
                }
            }

            return changed;
        }

        public async Task<bool> AreRatingsBalanced(DanceMusicCoreService dms)
        {
            var balanced = true;
            foreach (var pair in UserChunks)
            {
                balanced &= !await AreRatingsBalanced(pair.Key, pair.Value, dms);
            }

            return balanced;
        }

        private async Task<bool> AreRatingsBalanced(string user, List<SongChunk> chunks, DanceMusicCoreService dms)
        {
            var song = await Song.Create(Guid.NewGuid(), [.. chunks.SelectMany(chunk => chunk.Properties).Select(p => new SongProperty(p))], dms);
            var balanced = true;
            var tagSet = song.TagSummary.GetTagSet("Dance");

            foreach (var rating in song.DanceRatings)
            {
                if (rating.Weight != 1 && rating.Weight != 1)
                {
                    Trace.WriteLine($"{SongId}, Unbalanced rating ({user}): {rating.DanceId}={rating.Weight}");
                    balanced = false;
                }
                else
                {
                    var name = Dances.Instance.DanceFromId(rating.DanceId)?.Name;
                    if (name == null)
                    {
                        Trace.WriteLine($"{SongId}, Invalid DanceId ({user}): {rating.DanceId}");
                        balanced = false;
                    }
                    else
                    {
                        if (rating.Weight == -1)
                        {
                            name = $"!{name}";
                        }
                        if (!tagSet.Contains(name))
                        {
                            Trace.WriteLine($"{SongId}, Missing tag ({user}): {name}");
                            balanced = false;
                        }
                        else
                        {
                            tagSet.Remove(name);
                        }
                    }
                }
            }

            if (tagSet.Count > 0)
            {
                Trace.WriteLine($"{SongId}, Missing rating(s) ({user}): {string.Join(",", tagSet.ToArray())}");
                balanced = false;
            }

            return balanced;
        }

        private async Task<List<SongChunk>> FilterUnbalanced(string user, List<SongChunk> chunks, DanceMusicCoreService dms)
        {
            return chunks == null ? [] : await System.Linq.AsyncEnumerable.ToAsyncEnumerable(chunks)
                    .WhereAwait(async x => !await AreRatingsBalanced(user, [x], dms))
                    .ToListAsync();
        }

        public async Task<bool> MergeChunks(DanceMusicCoreService dms)
        {
            var changed = false;

            foreach (var pair in UserChunks.Where(p => IsPsuedoUser(p.Key) && !IsDwts(p.Key)))
            {
                var badChunks = await FilterUnbalanced(pair.Key, pair.Value, dms);

                var c = badChunks.Count;
                if (c < 2)
                {
                    continue;
                }
                var first = badChunks[0];
                var remove = new List<SongChunk>();
                for (var i = 1; i < c; i++)
                {
                    var chunk = badChunks[i];
                    if (!chunk.CheckHeader())
                    {
                        Trace.WriteLine($"{SongId}: Invalid header on {chunk.User}");
                        continue;
                    }
                    first.Properties.AddRange(chunk.Properties[3..]);
                    remove.Add(chunk);
                    changed = true;
                }
                foreach (var chunk in remove)
                {
                    pair.Value.Remove(chunk);
                    Chunks.Remove(chunk);
                }
            }

            return changed;
        }

        private void Chunk(IEnumerable<SongProperty> properties)
        {
            Chunks.Clear();
            UserChunks.Clear();

            int id = 1;
            var chunk = new List<SongProperty>();
            string user = null;

            foreach (var property in properties)
            {
                if (property.IsAction)
                {
                    if (chunk.Count != 0)
                    {
                        AddChunk(id++, user, chunk);
                        chunk.Clear();
                        user = null;
                    }
                }
                chunk.Add(property);
                if (property.BaseName == Song.UserField)
                {
                    if (user == null)
                    {
                        user = property.Value;
                    }
                    else
                    {
                        throw new Exception("Multiple users in a chunk");
                    }
                }
            }

            if (chunk.Count != 0)
            {
                AddChunk(id++, user, chunk);
            }
        }

        private void AddChunk(int id, string user, List<SongProperty> chunk)
        {
            var sc = new SongChunk(id, chunk);
            Chunks.Add(sc);
            AddUserChunk(user, sc);
        }

        private void AddUserChunk(string user, SongChunk chunk)
        {
            if (user == null)
            {
                if (chunk.Properties.Count > 1)
                {
                    throw new Exception("No user in a chunk");
                }
                return;
            }

            if (!UserChunks.TryGetValue(user, out var value))
            {
                value = ([]);
                UserChunks[user] = value;
            }

            value.Add(chunk);
        }

        private List<SongChunk> GetBatchChunks()
        {
            var chunks = new List<SongChunk>();
            foreach (var pair in UserChunks)
            {
                if (IsBatch(pair.Key))
                {
                    chunks.AddRange(pair.Value);
                }
            }
            return chunks;
        }

        private static bool IsBatch(string user) =>
            user != null && (user.Equals("batch|P", StringComparison.OrdinalIgnoreCase) || user.StartsWith("batch-", StringComparison.OrdinalIgnoreCase));

        private static bool IsPsuedoUser(string user) =>
            user != null && !user.StartsWith("batch", StringComparison.OrdinalIgnoreCase) && user.EndsWith("|P");

        private static bool IsDwts(string user) =>
            user != null && user.Equals("DWTS|P", StringComparison.OrdinalIgnoreCase);

    }
}
