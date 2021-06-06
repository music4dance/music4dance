using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DanceLibrary
{
    public interface ITempStorage
    {
        TextReader GetTextReader(string fileName);
        TextWriter GetTextWriter(string fileName);
    }
}