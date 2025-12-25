using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ATT_Wrapper.Components;

namespace ATT_Wrapper.Parsing
    {
    public interface ILogParser
        {
        bool ParseLine(string line, string statusFromLine,
                      Action<string, string, string> onResult,
                      Action<string> onProgress);
        }
    }
