﻿using System.Collections.Generic;
using System.Linq;

namespace kOS.Suffixed
{
    public class ElementValue : SpecialValue
    {
        private readonly string name;
        private readonly IList<Part> parts;
        private readonly uint uid;

        public ElementValue(IEnumerable<Part> parts)
        {
            this.parts = parts.ToList();
            var vessel = this.parts.First().vessel;
            name = vessel.name;
            uid = vessel.rootPart.uid;
        }

        public override object GetSuffix(string suffixName)
        {
            switch (suffixName)
            {
                case "NAME":
                    return name;
                case "ID":
                    return uid;
                case "PARTCOUNT":
                    return parts.Count;
                case "RESOURCES":
                    return ResourceValue.PartsToList(parts);
            }
            return base.GetSuffix(suffixName);
        }

        public static ListValue PartsToList(IEnumerable<Part> parts)
        {
            var toReturn = new ListValue();

            foreach (var flightParts in parts.GroupBy(p => p.flightID))
            {
                var element = new ElementValue(flightParts);
            }
            return toReturn;
        }
    }
}