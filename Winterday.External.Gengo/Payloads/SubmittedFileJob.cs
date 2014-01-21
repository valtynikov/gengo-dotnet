using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winterday.External.Gengo.Payloads
{
    public class SubmittedFileJob : SubmittedJob
    {
        public string TranslatedFileUrl { get; private set; }

        internal SubmittedFileJob(JObject json) : base(json)
        {
            TranslatedFileUrl = json.Value<string>("tgt_file_link");
        }
    }
}
