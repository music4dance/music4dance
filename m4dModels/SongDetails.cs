//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Globalization;
//using System.IO;
//using System.Linq;
//using System.Runtime.Serialization;
//using System.Runtime.Serialization.Json;
//using System.Text;
//using System.Text.RegularExpressions;
//using DanceLibrary;
//using Microsoft.Azure.Search.Models;
//using DataType = Microsoft.Azure.Search.Models.DataType;

//namespace m4dModels
//{
//    // This is a transitory object (really a ViewModel object) that is used for 
//    // viewing and editing a song, it shouldn't ever end up in a database,
//    // it's meant to aggregate the information about a song in an easily digestible way
//    [DataContract]
//    [KnownType(typeof(DanceRatingInfo))]
//    public class SongDetails : SongBase
//    {
//        #region Properties

//        // DBKILL: Don't need flat album anymore???
//        //public override string Album
//        //{
//        //    get
//        //    {
//        //        if (Albums != null && Albums.Count > 0)
//        //        {
//        //            return Albums[0].AlbumTrack;
//        //        }
//        //        return null;
//        //    }
//        //    set
//        //    {
//        //        throw new NotImplementedException("Album shouldn't be set directly in SongDetails");
//        //    }
//        //}



//        #endregion

//    }
//}
