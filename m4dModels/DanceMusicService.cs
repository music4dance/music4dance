using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace m4dModels
{
    public class DanceMusicService : DanceMusicCoreService
    {
        public DanceMusicService(DanceMusicContext context,
            UserManager<ApplicationUser> userManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            SongIndex songIndex = null) : base(
            context, searchService, danceStatsManager, songIndex)
        {
            UserManager = userManager;
        }

        public UserManager<ApplicationUser> UserManager { get; }

        #region Load

        private const string SongBreak = "+++++SONGS+++++";
        private const string TagBreak = "+++++TAGSS+++++";
        private const string SearchBreak = "+++++SEARCHES+++++";
        private const string DanceBreak = "+++++DANCES+++++";
        private const string PlaylistBreak = "+++++PLAYLISTS+++++";

        private const string UserHeader =
            "UserId\tUserName\tRoles\tPWHash\tSecStamp\tLockout\tProviders\tEmail\tEmailConfirmed\tStartDate\tRegion\tPrivacy\tCanContact\tServicePreference\tLastActive\tRowCount\tColumns\tSubscriptionLevel\tSubscriptionStart\tSubscriptionEnd";

        public static bool IsSongBreak(string line)
        {
            return IsBreak(line, SongBreak);
        }

        public static bool IsTagBreak(string line)
        {
            return IsBreak(line, TagBreak);
        }

        public static bool IsPlaylistBreak(string line)
        {
            return IsBreak(line, PlaylistBreak);
        }

        public static bool IsUserBreak(string line)
        {
            return UserHeader.StartsWith(line.Trim());
        }

        public static bool IsDanceBreak(string line)
        {
            return IsBreak(line, DanceBreak);
        }

        public static bool IsSearchBreak(string line)
        {
            return IsBreak(line, SearchBreak);
        }

        private static bool IsBreak(string line, string brk)
        {
            return string.Equals(line.Trim(), brk, StringComparison.InvariantCultureIgnoreCase);
        }

        private static void LogIdentityResult(ApplicationUser user, IdentityResult identityResult)
        {
            if (!identityResult.Succeeded)
            {
                Trace.WriteLineIf(
                    TraceLevels.General.TraceInfo,
                    $"Failed to create {user.UserName}.  {string.Join(',', identityResult.Errors.Select(ir => ir.Description))}");
            }
        }

        public async Task LoadUsers(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadUsers");
            var exclude = ":;,|@".ToHashSet();
            if (lines == null || lines.Count < 1 || !IsUserBreak(lines[0]))
            {
                throw new ArgumentOutOfRangeException();
            }

            var fieldCount = lines[0].Split('\t').Length;
            var i = 1;
            while (i < lines.Count)
            {
                AdminMonitor.UpdateTask("LoadUsers", i - 1);
                var s = lines[i];
                i += 1;

                if (string.Equals(s, TagBreak, StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }

                var cells = s.Split('\t');
                if (cells.Length != fieldCount)
                {
                    continue;
                }

                var userId = cells[0];
                var userName = cells[1];
                var roles = cells[2];
                var hash = string.IsNullOrWhiteSpace(cells[3]) ? null : cells[3];
                var stamp = cells[4];
                var lockout = cells[5];
                var providers = cells[6];
                string email = null;
                var emailConfirmed = false;
                var date = new DateTime();
                string region = null;
                byte privacy = 0;
                var canContact = ContactStatus.None;
                string servicePreference = null;
                var active = new DateTime();
                int? rc = null;
                string col = null;
                var subscriptionLevel = SubscriptionLevel.None;
                DateTime? subscriptionStart = null;
                DateTime? subscriptionEnd = null;
                decimal lifeTimePurchased = 0;

                var extended = cells.Length >= 17;
                if (extended)
                {
                    email = cells[7];
                    bool.TryParse(cells[8], out emailConfirmed);
                    DateTime.TryParse(cells[9], out date);
                    region = cells[10];
                    byte.TryParse(cells[11], out privacy);
                    byte.TryParse(cells[12], out var canContactT);
                    canContact = (ContactStatus)canContactT;
                    servicePreference = cells[13];
                    DateTime.TryParse(cells[14], out active);
                    if (!string.IsNullOrWhiteSpace(cells[15]) &&
                        int.TryParse(cells[15], out var rcT))
                    {
                        rc = rcT;
                    }

                    if (!string.IsNullOrWhiteSpace(cells[16]))
                    {
                        col = cells[16];
                    }
                }

                if (userName.Any(x => exclude.Contains(x)))
                {
                    Trace.WriteLine($"Invalid username {userName}");
                    continue;
                }

                if (cells.Length >= 20 && Enum.TryParse(cells[17], out subscriptionLevel) &&
                    subscriptionLevel != SubscriptionLevel.None)
                {
                    if (DateTime.TryParse(cells[18], out var start))
                    {
                        subscriptionStart = start;
                    }

                    if (DateTime.TryParse(cells[19], out var end))
                    {
                        subscriptionEnd = end;
                    }

                    if (cells.Length >= 20)
                    {
                        decimal.TryParse(cells[20], out lifeTimePurchased);
                    }
                }

                var user = await UserManager.FindByIdAsync(userId) ??
                    await UserManager.FindByNameAsync(userName);

                var create = user == null;

                if (create)
                {
                    user = new ApplicationUser
                    {
                        Id = userId,
                        UserName = userName,
                        NormalizedUserName = userName.ToUpperInvariant(),
                        PasswordHash = hash,
                        SecurityStamp = stamp,
                        LockoutEnabled = string.Equals(
                            lockout, "TRUE",
                            StringComparison.InvariantCultureIgnoreCase),
                        ConcurrencyStamp = Guid.NewGuid().ToString()
                    };

                    if (extended)
                    {
                        user.StartDate = date;
                        user.Email = email;
                        user.NormalizedEmail = email.ToUpperInvariant();
                        user.EmailConfirmed = emailConfirmed;
                        user.Region = region;
                        user.Privacy = privacy;
                        user.CanContact = canContact;
                        user.ServicePreference = servicePreference;
                    }

                    LogIdentityResult(user, await UserManager.CreateAsync(user));
                }
                else if (extended)
                {
                    if (string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(email))
                    {
                        user.Email = email;
                        user.EmailConfirmed = emailConfirmed;
                        user.NormalizedEmail = email.ToUpperInvariant();
                    }

                    if (string.IsNullOrWhiteSpace(user.Region) &&
                        !string.IsNullOrWhiteSpace(region))
                    {
                        user.Region = region;
                        user.Privacy = privacy;
                        user.CanContact = canContact;
                        user.ServicePreference = servicePreference;
                    }

                    user.ConcurrencyStamp = Guid.NewGuid().ToString();

                    var logins = await UserManager.GetLoginsAsync(user);
                    foreach (var login in logins)
                    {
                        await UserManager
                            .RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
                    }

                    var rolesT = await UserManager.GetRolesAsync(user);
                    foreach (var role in rolesT)
                    {
                        LogIdentityResult(user, await UserManager.RemoveFromRoleAsync(user, role));
                    }
                }

                user.LastActive = active;
                user.RowCountDefault = rc;
                user.ColumnDefaults = col;
                user.SubscriptionLevel = subscriptionLevel;
                user.SubscriptionStart = subscriptionStart;
                user.SubscriptionEnd = subscriptionEnd;
                user.LifetimePurchased = lifeTimePurchased;

                if (!string.IsNullOrWhiteSpace(providers))
                {
                    var entries =
                        providers.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    for (var j = 0; j < entries.Length; j += 2)
                    {
                        var login = new UserLoginInfo(entries[j], entries[j + 1], entries[j]);
                        LogIdentityResult(user, await UserManager.AddLoginAsync(user, login));
                    }
                }

                if (!string.IsNullOrWhiteSpace(roles))
                {
                    var roleNames = roles.Split(
                        new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var roleName in roleNames)
                    {
                        LogIdentityResult(user, await UserManager.AddToRoleAsync(user, roleName));
                    }
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
            await SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadUsers");
        }

        public async Task LoadSearches(IList<string> lines, bool reload = false)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadSearches");

            if (lines == null || lines.Count < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (lines.Count > 0 && IsSearchBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            if (lines.Count > 0)
            {
                if (reload)
                {
                    await LoadSearchesBulk(lines);
                }
                else
                {
                    await LoadSearchesIncremental(lines);
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
            await SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadSearches");
        }

        private async Task LoadSearchesIncremental(IList<string> lines)
        {
            var fieldCount = lines[0].Split('\t').Length;
            for (var i = 0; i < lines.Count; i++)
            {
                AdminMonitor.UpdateTask("LoadSearches", i);
                var s = lines[i];

                if (string.Equals(s, TagBreak, StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }

                var newSearch = await ParseSearchEntry(s, fieldCount);
                if (newSearch == null)
                {
                    continue;
                }

                var search = newSearch.ApplicationUser == null
                    ? Searches.FirstOrDefault(
                        x => x.ApplicationUser == null && x.Query == newSearch.Query)
                    : Searches.FirstOrDefault(
                        x =>
                            x.ApplicationUser != null &&
                            x.ApplicationUser.Id == newSearch.ApplicationUserId &&
                            x.Query == newSearch.Query);

                if (search == null)
                {
                    Searches.Add(newSearch);
                }
                else
                {
                    search.Update(
                        newSearch.ApplicationUser, newSearch.Name, newSearch.Query,
                        newSearch.Favorite, newSearch.Count, newSearch.Created, newSearch.Modified);
                }
            }
        }

        private async Task LoadSearchesBulk(IList<string> lines)
        {
            try
            {
                Context.AutoDetectChangesEnabled = false;

                var fieldCount = lines[0].Split('\t').Length;
                for (var i = 0; i < lines.Count; i++)
                {
                    AdminMonitor.UpdateTask("LoadSearches", i);
                    var s = lines[i];

                    if (string.Equals(s, TagBreak, StringComparison.InvariantCultureIgnoreCase))
                    {
                        break;
                    }

                    var search = await ParseSearchEntry(s, fieldCount);
                    if (search == null)
                    {
                        continue;
                    }

                    Searches.Add(search);
                }
            }
            finally
            {
                Context.AutoDetectChangesEnabled = true;
            }
        }


        private async Task<Search> ParseSearchEntry(string line, int fieldCount)
        {
            var search = new Search();
            var cells = line.Split('\t');
            if (cells.Length != fieldCount)
            {
                return null;
            }

            var userName = cells[0];
            search.Name = cells[1];
            search.Query = cells[2];
            search.Favorite = string.Equals(cells[3], "true", StringComparison.OrdinalIgnoreCase);
            if (int.TryParse(cells[4], out var count))
            {
                search.Count = count;
            }

            if (DateTime.TryParse(cells[5], out var created))
            {
                search.Created = created;
            }

            if (DateTime.TryParse(cells[6], out var modified))
            {
                search.Modified = modified;
            }

            search.ApplicationUser =
                string.IsNullOrWhiteSpace(userName) ? null : await FindUser(userName);

            return search;
        }

        public async Task LoadDances(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering Dances");

            if (lines.Count > 0 && IsDanceBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            AdminMonitor.UpdateTask("LoadDances");
            await LoadDances();
            var modified = false;
            for (var index = 0; index < lines.Count; index++)
            {
                var s = lines[index];
                AdminMonitor.UpdateTask("LoadDances", index + 1);
                if (string.IsNullOrWhiteSpace(s))
                {
                    continue;
                }

                var cells = s.Split('\t').ToList();
                var d = await Dances.FindAsync(cells[0]);
                if (d == null)
                {
                    d = new Dance
                    {
                        Id = cells[0]
                    };
                    Dances.Add(d);
                    modified = true;
                }

                cells.RemoveAt(0);
                modified |= d.Update(cells);
            }

            if (modified)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
                await SaveChanges();
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting Dances");
        }

        public async Task LoadTags(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadTags");

            for (var index = 0; index < lines.Count; index++)
            {
                var s = lines[index];
                AdminMonitor.UpdateTask("LoadTags", index + 1);

                var cells = s.Split('\t');
                TagGroup tt = null;
                if (cells.Length >= 2)
                {
                    var category = cells[0];
                    var value = cells[1];
                    var key = TagGroup.BuildKey(value, category);

                    var ttOld = await TagGroups.FindAsync(key); // ?? TagMap.GetValueOrDefault(key);

                    if (ttOld != null)
                    {
                        if (ttOld.Key != key)
                        {
                            TagGroups.Remove(ttOld);
                            ttOld = null;
                        }
                    }

                    if (ttOld == null)
                    {
                        tt = CreateTagGroup(value, category, false);
                    }
                }

                if (tt != null && cells.Length >= 3 && !string.IsNullOrWhiteSpace(cells[2]))
                {
                    tt.PrimaryId = cells[2];
                }

                if (tt != null)
                {
                    if (cells.Length >= 4 &&
                        !string.IsNullOrWhiteSpace(cells[3]) &&
                        DateTime.TryParse(cells[3], out var modified))
                    {
                        tt.Modified = modified;
                    }
                    else
                    {
                        tt.Modified = DateTime.MinValue;
                    }
                }
            }

            foreach (var tt in TagGroups)
            {
                tt.Children = null;
            }

            foreach (var tt in TagGroups)
            {
                if (string.IsNullOrEmpty(tt.PrimaryId))
                {
                    tt.Primary = null;
                }
                else
                {
                    tt.Primary = await TagGroups.FindAsync(tt.PrimaryId);
                    if (tt.Primary.Children == null)
                    {
                        tt.Primary.AddChild(tt);
                    }
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
            await SaveChanges();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadTags");
        }

        public async Task LoadPlaylists(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadPlaylists");
            var now = DateTime.Now;

            for (var index = 0; index < lines.Count; index++)
            {
                var s = lines[index].Trim();
                AdminMonitor.UpdateTask("LoadPlaylists", index + 1);

                if (string.Equals(PlaylistBreak, s))
                {
                    continue;
                }

                var cells = s.Split('\t');

                var created = now;
                DateTime? modified = null;
                var deleted = false;
                string data2 = null;
                string data1;
                string id;
                string name = null;
                string description = null;

                var type = PlayListType.SongsFromSpotify;

                if (cells.Length < 3)
                {
                    continue;
                }

                // m4dId
                var userId = cells[0];


                // This is a special case for SongFromSpotify [m4did,DanceTags,url]
                if ((cells.Length == 3 || cells.Length == 4) &&
                    type == PlayListType.SongsFromSpotify)
                {
                    var r = new Regex(
                        @"https://open.spotify.com/(user/(?<user>[a-z0-9-]*)/)?playlist/(?<id>[a-z0-9]*)",
                        RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
                    var m = r.Match(cells[2]);
                    if (!m.Success)
                    {
                        continue;
                    }

                    id = m.Groups["id"].Value;
                    var email = cells.Length == 4 ? cells[3] : m.Groups["user"].Value;

                    await FindOrAddUser(userId, PseudoRole, email + "@spotify.com");
                    data1 = cells[1];
                }
                else
                {
                    if (cells.Length < 5)
                    {
                        continue;
                    }

                    // Type
                    // TODO: Once this is published to the server, we can get rid of this check for legacy type "Spotify"
                    if (!string.Equals(cells[1], "Spotify"))
                    {
                        Enum.TryParse(cells[1], out type);
                    }

                    // Dance/tags
                    data1 = cells[2];

                    // Spotify Playlist Id
                    id = cells[3];

                    DateTime.TryParse(cells[4], out created);
                    if (cells.Length > 5 && DateTime.TryParse(cells[5], out var mod))
                    {
                        modified = mod;
                    }

                    if (cells.Length > 6)
                    {
                        bool.TryParse(cells[6], out deleted);
                    }

                    if (cells.Length > 7)
                    {
                        data2 = cells[7];
                    }

                    if (cells.Length > 8)
                    {
                        name = cells[8];
                    }

                    if (cells.Length > 9)
                    {
                        description = cells[9];
                    }
                }

                var playlist = await PlayLists.FindAsync(id);
                var isNew = playlist == null;
                if (isNew)
                {
                    playlist = new PlayList();
                }

                playlist.Id = id;
                playlist.Type = type;
                playlist.Data1 = data1;
                playlist.User = userId;
                playlist.Created = created;
                playlist.Updated = modified;
                playlist.Deleted = deleted;
                playlist.Data2 = data2;
                playlist.Name = name;
                playlist.Description = description;

                if (isNew)
                {
                    PlayLists.Add(playlist);
                }
            }

            await SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadPlaylists");
        }

        public async Task LoadSongs(IList<string> lines)
        {
            // Load the dance List
            await LoadDances();

            var c = 0;
            foreach (var line in lines)
            {
                AdminMonitor.UpdateTask("LoadSongs", c);
                var time = DateTime.Now;
                var song = new Song { Created = time, Modified = time };

                await song.Load(line, this);

                c += 1;

                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving next 100 songs");
                }
            }
        }

        public async Task UpdateSongs(IList<string> lines, bool clearCache = true)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering UpdateSongs");

            // Load the dance List
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Dances");
            await LoadDances();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Songs");

            if (lines.Count > 0 && IsSongBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            var c = 0;
            foreach (var line in lines.Where(line => !line.StartsWith("//")))
            {
                AdminMonitor.UpdateTask("UpdateSongs", c);

                var sd = await Song.Create(line, this);
                var song = await SongIndex.FindSong(sd.SongId);

                if (song == null)
                {
                    var up = sd.FirstProperty(Song.UserField);
                    var user = await FindOrAddUser(up != null ? up.Value : "batch", EditRole);

                    song = SongIndex.CreateSong(sd.SongId);
                    await SongIndex.UpdateSong(user, song, sd);

                    // This was a merge so delete the input songs
                    if (sd.SongProperties.Count > 0 &&
                        sd.SongProperties[0].Name == Song.MergeCommand)
                    {
                        var list = await SongIndex.SongsFromList(sd.SongProperties[0].Value);
                        foreach (var s in list)
                        {
                            await SongIndex.DeleteSong(user, s);
                        }
                    }
                }
                else
                {
                    var up = sd.LastProperty(Song.UserField);
                    var user = await FindOrAddUser(up != null ? up.Value : "batch", EditRole);
                    if (sd.IsNull)
                    {
                        await SongIndex.DeleteSong(user, song);
                    }
                    else
                    {
                        await SongIndex.UpdateSong(user, song, sd);
                    }
                }

                c += 1;
                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{c} songs updated");
                }
            }

            if (clearCache)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Clearing Song Cache");
                await DanceStatsManager.ClearCache(this, true);
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting UpdateSongs");
        }

        public async Task AdminUpdate(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering AdminUpdate");

            // Load the dance List
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Dances");
            await LoadDances();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Songs");

            if (lines.Count > 0 && IsSongBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            var c = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("//"))
                {
                    continue;
                }

                AdminMonitor.UpdateTask("UpdateSongs", c);

                await SongIndex.AdminEditSong(line);

                c += 1;
                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{c} songs updated");
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Clearing Song Cache");
            await DanceStatsManager.ClearCache(this, true);
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting AdminUpdate");
        }

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        // ReSharper disable once UnusedMember.Local
        private static readonly Guid s_guidError = new("25053e8c-5f1e-441e-bd54-afdab5b1b638");


        public void SeedDances()
        {
            var dances = DanceLibrary.Dances.Instance;
            foreach (var dance in from d in dances.AllDanceGroups
                let dance = Context.Dances.Find(d.Id)
                where dance == null
                select new Dance { Id = d.Id })
            {
                Context.Dances.Add(dance);
            }

            foreach (var dance in from d in dances.AllDanceTypes
                let dance = Context.Dances.Find(d.Id)
                where dance == null
                select new Dance { Id = d.Id })
            {
                Context.Dances.Add(dance);
            }
        }

        private async Task LoadDances()
        {
            await Context.LoadDances();
        }

        #endregion

        #region Save

        public IList<string> SerializeUsers(bool withHeader = true, DateTime? from = null)
        {
            var users = new List<string>();

            if (!from.HasValue)
            {
                from = new DateTime(1, 1, 1);
            }

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var user in UserManager.Users.Where(u => u.LastActive >= from.Value)
                .OrderByDescending(u => u.LastActive > u.StartDate ? u.LastActive : u.StartDate)
                .ToList())
            {
                var userId = user.Id;
                var username = user.UserName;
                var roles = string.Join("|", UserManager.GetRolesAsync(user).Result);
                var hash = user.PasswordHash;
                var stamp = user.SecurityStamp;
                var lockout = user.LockoutEnabled.ToString();
                var providers = string.Join(
                    "|",
                    UserManager.GetLoginsAsync(user).Result
                        .Select(l => l.LoginProvider + "|" + l.ProviderKey));
                var email = user.Email;
                var emailConfirmed = user.EmailConfirmed;
                var time = user.StartDate.ToString("g");
                var region = user.Region;
                var privacy = user.Privacy.ToString();
                var canContact = ((byte)user.CanContact).ToString();
                var servicePreference = user.ServicePreference;
                var lastActive = user.LastActive.ToString("g");
                var rc = user.RowCountDefault;
                var col = user.ColumnDefaults;
                var sl = user.SubscriptionLevel;
                var ss = user.SubscriptionStart;
                var se = user.SubscriptionEnd;
                var lp = user.LifetimePurchased;

                users.Add(
                    $"{userId}\t{username}\t{roles}\t{hash}\t{stamp}\t{lockout}\t{providers}\t{email}\t{emailConfirmed}\t{time}\t{region}\t{privacy}\t{canContact}\t{servicePreference}\t{lastActive}\t{rc}\t{col}\t{sl}\t{ss}\t{se}\t{lp}");
            }

            if (withHeader && users.Count > 0)
            {
                users.Insert(0, UserHeader);
            }

            return users;
        }

        public IList<string> SerializeTags(bool withHeader = true, DateTime? from = null)
        {
            var tags = new List<string>();

            if (!from.HasValue)
            {
                from = new DateTime(1, 1, 1);
            }

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var tt in TagGroups.Where(t => t.Modified >= from.Value)
                .OrderBy(t => t.Modified))
            {
                tags.Add($"{tt.Category}\t{tt.Value}\t{tt.PrimaryId}\t{tt.Modified:g}");
            }

            if (withHeader && tags.Count > 0)
            {
                tags.Insert(0, TagBreak);
            }

            return tags;
        }

        public IList<string> SerializeSearches(bool withHeader = true, DateTime? from = null)
        {
            var searches = new List<string>();

            if (!from.HasValue)
            {
                from = new DateTime(1, 1, 1);
            }

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var search in Searches.Include(s => s.ApplicationUser)
                .Where(s => s.Modified >= from.Value).OrderBy(s => s.Modified))
            {
                var userName = search.ApplicationUser != null
                    ? search.ApplicationUser.UserName
                    : string.Empty;
                searches.Add(
                    $"{userName}\t{search.Name}\t{search.Query}\t{search.Favorite}\t{search.Count}\t{search.Created:g}\t{search.Modified:g}");
            }

            if (withHeader && searches.Count > 0)
            {
                searches.Insert(0, SearchBreak);
            }

            return searches;
        }

        public async Task<IList<string>> SerializeSongs(bool withHeader = true,
            bool withHistory = true,
            int max = -1, DateTime? from = null, SongFilter filter = null,
            HashSet<Guid> exclusions = null)
        {
            var songs = new List<string>();

            if (withHeader)
            {
                songs.Add(SongBreak);
            }

            songs.AddRange(await SongIndex.BackupIndex(max, filter));

            return songs;
        }

        [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
        public IList<string> SerializePlaylists(bool withHeader = true, DateTime? from = null)
        {
            if (!from.HasValue)
            {
                from = new DateTime(1, 1, 1);
            }

            var playlists = PlayLists
                .Where(
                    d =>
                        d.Updated.HasValue && d.Updated >= from.Value || d.Created >= from.Value)
                .OrderBy(d => d.Updated).ThenBy(d => d.Created);

            var lines = new List<string>();
            foreach (var p in playlists)
            {
                lines.Add(
                    $"{p.User}\t{p.Type}\t{p.Data1}\t{p.Id}\t{p.Created}\t{p.Updated}\t{p.Deleted}\t{p.Data2}\t{p.Name}\t{p.Description}");
            }

            if (withHeader && lines.Count > 0)
            {
                lines.Insert(0, PlaylistBreak);
            }

            return lines;
        }

        public IList<string> SerializeDances(bool withHeader = true, DateTime? from = null)
        {
            var dances = new List<string>();

            if (!from.HasValue)
            {
                from = new DateTime(1, 1, 1);
            }

            var dancelist = Dances.Where(d => d.Modified >= from.Value)
                .Include(dance => dance.DanceLinks).OrderBy(d => d.Modified).ToList();
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var dance in dancelist)
            {
                var line = dance.Serialize();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    dances.Add(line);
                }
            }

            if (withHeader && dances.Count > 0)
            {
                dances.Insert(0, DanceBreak);
            }

            return dances;
        }

        #endregion

        #region User

        protected override async Task<ApplicationUser> CoreFindUser(string name)
        {
            return await UserManager.FindByNameAsync(name);
        }

        public async Task<ApplicationUser> FindOrAddUser(string name, string role = null,
            string email = null)
        {
            var user = await FindUser(name);

            if (user == null)
            {
                email ??= name + "@music4dance.net";

                user = new ApplicationUser
                {
                    UserName = name, Email = email, EmailConfirmed = true, StartDate = DateTime.Now
                };

                // ASYNCTODO: I should really propagate the aync all the way to the controller
                var res = UserManager.CreateAsync(user, "_This_Is_@_placeh0lder_").Result;
                if (res.Succeeded)
                {
                    var user2 = await FindUser(name);
                    Trace.WriteLine($"{user2.UserName}:{user2.Id}");
                }
            }

            if (string.Equals(role, PseudoRole))
            {
                user.LockoutEnabled = true;
            }
            else
            {
                AddRole(user.Id, role);
            }

            return user;
        }

        public async Task ChangeUserName(string oldUserName, string userName)
        {
            var songs = (await SongIndex.FindUserSongs(oldUserName, true)).ToList();

            foreach (var song in songs)
            {
                var props = new List<SongProperty>(song.SongProperties);
                foreach (var prop in props.Where(
                    p =>
                        (p.Name == Song.UserField || p.Name == Song.UserProxy) &&
                        p.Value == oldUserName))
                {
                    prop.Value = userName;
                }

                await song.AdminEdit(props, this);
            }

            await SongIndex.SaveSongs(songs);
        }

        private void AddRole(string id, string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return;
            }

            var key = id + ":" + role;
            if (_roleCache.Contains(key))
            {
                return;
            }

            var user = UserManager.FindByIdAsync(id).Result;
            if (!UserManager.IsInRoleAsync(user, role).Result)
            {
                UserManager.AddToRoleAsync(user, role).Wait();
            }

            _roleCache.Add(key);
        }

        private readonly HashSet<string> _roleCache = new();

        #endregion
    }
}
