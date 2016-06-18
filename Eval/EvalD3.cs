using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DecisionServiceWebAPI.Eval
{
    public class EvalD3
    {
        public string key { get; set; }

        public Dictionary<DateTime, float> values { get; set; }
    }
}