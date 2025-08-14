using GridifyExtensions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GridifyExtensions.Helpers;
internal static class GridifyEscapeHelper
{

   public static string? ReplaceSpecialChars(string? filter)
   {
      if (string.IsNullOrWhiteSpace(filter))
      {
         return filter;
      }

      var esc = Regex.Replace(filter, @"(?<!\\)([(),|$]|/i)", @"\$1");

      return esc;
   }
}