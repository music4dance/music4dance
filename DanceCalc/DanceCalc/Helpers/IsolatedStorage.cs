// Copyright (c) 2010 Microsoft Corporation.  All rights reserved.
//
//
// Use of this source code is subject to the terms of the Microsoft
// license agreement under which you licensed this source code.
// If you did not accept the terms of the license agreement,
// you are not authorized to use this source code.
// For the terms of the license, please see the license agreement
// signed by you and Microsoft.
// THE SOURCE CODE IS PROVIDED "AS IS", WITH NO WARRANTIES OR INDEMNITIES.
//
using System;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using System.Xml.Serialization;

using DanceLibrary;

namespace DanceCalc
{

    /// <summary>
    /// Isolated storage file helper class
    /// </summary>
    /// <typeparam name="T">Data type to serialize/deserialize</typeparam>
    public class IsolatedStorage<T>
    {
        /// <summary>
        /// Lock object for thread pool save operations
        /// </summary>
        private object saveLock = new object();

        /// <summary>
        /// Begins a background thread save operation
        /// </summary>
        /// <param name="fileName">Name of the file to write to.</param>
        /// <param name="data">The data to store.</param>
        /// <param name="completed">Action delegate to cal on completion.</param>
        /// <param name="handleException">Exception handler.</param>
        public void BeginSave(
            string fileName, 
            T data ,
            Action completed, 
            Action<Exception> handleException )
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                try
                {
                    lock (saveLock)
                    {
                        this.SaveToFile(fileName, data);
                    }

                    if (completed != null)
                    {
                        completed();
                    }
                }
                catch (Exception e)
                {
                    if (handleException != null)
                    {
                        handleException(e);
                    }
                }
            });
        }



        /// <summary>
        /// Loads data from a file
        /// </summary>
        /// <param name="fileName">Name of the file to read.</param>
        /// <returns>Data object</returns>
        public T LoadFromFile(string fileName)
        {
            T loadedFile = default(T);
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists(fileName))
                    {
                        XmlSerializer mySerializer = new XmlSerializer(typeof(T));
                        using (FileStream myFileStream = store.OpenFile(fileName, FileMode.Open))
                        {
                            TextReader t = new StreamReader(myFileStream);
#if DEBUG
                            while (t.Peek() != -1)
                            {
                                string s = t.ReadLine();
                                Debug.WriteLine(s);
                            }
                            myFileStream.Seek(0, SeekOrigin.Begin);
#endif
                            // Call the Deserialize method and cast to the object type.
                            if (myFileStream.Length > 0)
                            {
                                loadedFile = (T)mySerializer.Deserialize(myFileStream);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                //ApplicationState.ErrorLog.Add(new ErrorLog("LoadFromFile", e.Message));
                Debug.WriteLine(e.Message);
            }
            return loadedFile;
        }

        /// <summary>
        /// Saves data to a file.
        /// </summary>
        /// <param name="fileName">Name of the file to write to</param>
        /// <param name="data">The data to save</param>
        public void SaveToFile(string fileName, T data)
        {
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    XmlSerializer mySerializer = new XmlSerializer(typeof(T));
                    if (store.FileExists(fileName))
                    {
                        store.DeleteFile(fileName);
                    }

                    using (StreamWriter myWriter =
                        new StreamWriter(store.OpenFile(fileName, FileMode.CreateNew)))
                    {
                        mySerializer.Serialize(myWriter, data);
                    }
                }
            }
            catch (Exception e)
            {
                // Add desired error handling for your application
                //ApplicationState.ErrorLog.Add(new ErrorLog("SaveToFile", e.Message));
                Debug.WriteLine(e.Message);
            }
        }
    }

    public class TempStorage : ITempStorage
    {
        public static TempStorage Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TempStorage();

                return _instance;
            }
        }

        private static TempStorage _instance;

        /// <summary>
        /// Gets a text reader on isolated storage
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public TextReader GetTextReader(string fileName)
        {
            TextReader t = null;

            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists(fileName))
                    {
                        FileStream myFileStream = store.OpenFile(fileName, FileMode.Open);
                        t = new StreamReader(myFileStream);
#if DEBUG
                        while (t.Peek() != -1)
                        {
                            string s = t.ReadLine();
                            Debug.WriteLine(s);
                        }
                        myFileStream.Seek(0, SeekOrigin.Begin);
#endif
                    }
                }
            }
            catch (Exception e)
            {
                //ApplicationState.ErrorLog.Add(new ErrorLog("LoadFromFile", e.Message));
                Debug.WriteLine(e.Message);
            }
            return t;
        }

        /// <summary>
        /// Get a text writer on isolated storage
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public TextWriter GetTextWriter(string fileName)
        {
            TextWriter t = null;
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists(fileName))
                    {
                        store.DeleteFile(fileName);
                    }

                    t = new StreamWriter(store.OpenFile(fileName, FileMode.CreateNew));
                }
            }
            catch (Exception e)
            {
                //ApplicationState.ErrorLog.Add(new ErrorLog("SaveToFile", e.Message));
                Debug.WriteLine(e.Message);
            }

            return t;
        }

        public void CleanStorage()
        {
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    string[] files = store.GetFileNames();

                    foreach (string file in files)
                    {
                        store.DeleteFile(file);
                    }
                }
            }
            catch (Exception e)
            {
                //ApplicationState.ErrorLog.Add(new ErrorLog("SaveToFile", e.Message));
                Debug.WriteLine(e.Message);
            }
        }
    }
}
