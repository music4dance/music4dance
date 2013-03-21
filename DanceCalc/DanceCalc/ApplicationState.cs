using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Shell;

using DanceLibrary;

namespace DanceCalc
{
    internal class ApplicationState
    {
        /// <summary>
        /// Key for the Main Page state storage
        /// </summary>
        internal const string MainPageState = "MainPageState";

        /// <summary>
        /// Key for the Filter state storage
        /// </summary>
        internal const string FilterState = "FilterState";

        /// <summary>
        /// Filename for main page state longer term storage
        /// </summary>
        internal const string StateFileName = "DCMainPageState.xml";

        /// <summary>
        /// Filename for main page state longer term storage
        /// </summary>
        internal const string FilterFileName = "DCFilterState.xml";

        public static MainPageState MainPageInfo { get; set; }

        /// <summary>
        /// Initial application initialization. Logic that is deferred related to 
        /// loading the supported conversions from a file in the xap to speed up
        /// 1st page render
        /// </summary>
        internal static void AppLaunchInitialization()
        {

            if (ApplicationState.MainPageInfo == null)
            {
                ApplicationState.MainPageInfo = new MainPageState();
            }
        }

        /// <summary>
        /// Adds the app state objects to the system object store
        /// </summary>
        /// 
        internal static void SaveAppObjects()
        {
            IsolatedStorage<MainPageState> f = new IsolatedStorage<MainPageState>();
            f.SaveToFile(ApplicationState.StateFileName, MainPageInfo);

            using (TextWriter t = TempStorage.Instance.GetTextWriter(ApplicationState.FilterFileName))
            {
                FilterObject.WriteState(t);
                t.Close();
            }
        }

        /// <summary>
        /// Adds the app state objects to the system object store
        /// </summary>
        /// 
        internal static void AddAppObjects()
        {
            AddObject(MainPageState, MainPageInfo);
            AddObject(FilterState, FilterObject.GetState());
        }

        /// <summary>
        /// Retrieves the app state objects from the isolated storage
        /// </summary>
        /// <returns>True if all objects are non null, false otherwise</returns>
        internal static bool LoadAppObjects()
        {
            bool allObjectsNonNull = true;

            IsolatedStorage<MainPageState> f = new IsolatedStorage<MainPageState>();
            MainPageState state = f.LoadFromFile(ApplicationState.StateFileName);

            using (TextReader t = TempStorage.Instance.GetTextReader(ApplicationState.FilterFileName))
            {
                if (t != null)
                {
                    FilterObject.ReadState(t);
                    t.Close();
                }
            }

            if (state != null)
            {
                MainPageInfo = state;
                allObjectsNonNull = false;
            }
            return allObjectsNonNull;
        }

        /// <summary>
        /// Retrieves the app state objects from the system object store
        /// </summary>
        /// <returns>True if all objects are non null, false otherwise</returns>
        internal static bool RetrieveAppObjects()
        {
            bool allObjectsNonNull = true;
            MainPageState state = RetrieveObject<MainPageState>(MainPageState);
            if (state != null)
            {
                MainPageInfo = state;
                allObjectsNonNull = false;
            }

            string s = RetrieveObject<string>(FilterState);
            if (s != null)
            {
                FilterObject.SetState(s);
            }

            return allObjectsNonNull;
        }

        /// <summary>
        /// Retrieves the specified object from the system object store
        /// </summary>
        /// <typeparam name="T">Data to retrieve</typeparam>
        /// <param name="key">The object key</param>
        /// <returns>object default, or deserialized object</returns>
        private static T RetrieveObject<T>(string key)
        {
            T data = default(T);
            if (PhoneApplicationService.Current.State.ContainsKey(key))
            {
                data = (T)PhoneApplicationService.Current.State[key];
            }
            return data;
        }

        /// <summary>
        /// Adds the specified data object to the system object store
        /// </summary>
        /// <param name="key">The object key.</param>
        /// <param name="data">The data to store.</param>
        private static void AddObject(string key, object data)
        {
            if (PhoneApplicationService.Current.State.ContainsKey(key))
            {
                PhoneApplicationService.Current.State.Remove(key);
            }
            PhoneApplicationService.Current.State.Add(key, data);
        }

    }
}
