using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cut.Lib.Extensions;

internal static class ObjectExtensions
{
    internal static DateTime FromInvariantDateTime(this object value)
    {
        if (value is DateTime dt) return dt;

        if (value is string dtString) return Convert.ToDateTime(dtString, CultureInfo.InvariantCulture);

        return Convert.ToDateTime(value);
    }
}