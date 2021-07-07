using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle ("Base45 Command line utility")]
[assembly: AssemblyDescription("A small utility that can decode and encode Base45 strings")]
[assembly: AssemblyConfiguration ("")]
[assembly: AssemblyCompany ("Jonas Kohl")]
[assembly: AssemblyProduct ("Base45 Command line utility")]
[assembly: AssemblyCopyright("Copyright © 2021 Jonas Kohl")]
[assembly: AssemblyTrademark ("")]
[assembly: AssemblyCulture ("")]
[assembly: ComVisible (false)]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: Guid ("77344de8-fbb2-4ebb-a824-68c3f1fcea08")]

namespace Base45 {
  public static class Program {
    private static void PrintLogo() {
      Console.WriteLine("Base45 Command line utility 1.0.0.0");
      Console.WriteLine("Copyright (c) 2021 Jonas Kohl");
      Console.WriteLine("");
    }
    
    private static void PrintUsage() {
      Console.WriteLine("Usage: Base45 <action> <source> [options...]");
      Console.WriteLine("   or: Base45 <-h|--help>");
      Console.WriteLine("");
      Console.WriteLine("Actions:");
      Console.WriteLine("  -d | --decode ....... Decodes a string (given in source)");
      Console.WriteLine("  -e | --encode ....... Encodes a string (given in source)");
      Console.WriteLine("  -r | --decode-file .. Decodes the contents of a file (filename given in source)");
      Console.WriteLine("  -w | --encode-file .. Encodes the contents of a file (filename given in source)");
      Console.WriteLine("Please note that this program will always output to STDOUT and never to a file.");
      Console.WriteLine("For -w and -r, the contents in the file have to be plaintext (not binary).");
      Console.WriteLine("");
      Console.WriteLine("Options:");
      Console.WriteLine("  --ignore-errors ..... Ignores all decoding errors. May lead to garbage output");
      Console.WriteLine("  --nologo ............ Prevents initial output of program information");
    }
    
    public static int Main(string[] args) {
      var ignoreErrors = false;
      var nologo = false;
      
      if (args.Length < 1) {
        PrintLogo();
        Console.Error.WriteLine("Missing 1 required positional argument at index 0: action");
        PrintUsage();
        return -1;
      }
      
      if (args[0] == "-h" || args[0] == "--help") {
        PrintLogo();
        PrintUsage();
        return 0;
      }
      
      if (args.Length < 2) {
        PrintLogo();
        Console.Error.WriteLine("Missing 1 required positional argument at index 1: inputStr");
        PrintUsage();
        return -1;
      }
      
      if (args.Length > 2) {
        ignoreErrors = args.Skip(2).Where(n => n == "--ignore-errors").Any();
        nologo = args.Skip(2).Where(n => n == "--nologo").Any();
      }
      
      if (!nologo)
        PrintLogo();
      
      var action = args[0];
      var inputStr = args[1];
      
      try {
        if (action == "--decode" || action == "-d") {
          Console.Write(Encoding.ASCII.GetString(Base45Encoding.Decode(inputStr, ignoreErrors)));
        } else if (action == "--encode" || action == "-e") {
          Console.Write(Base45Encoding.Encode(Encoding.ASCII.GetBytes(inputStr)));
        } else if (action == "--decode-file" || action == "-r") {
          if (!File.Exists(inputStr)) {
            Console.Error.WriteLine("File " + inputStr + " does not exist");
            return -3;
          }
          var contents = File.ReadAllText(inputStr);
          Console.Write(Encoding.ASCII.GetString(Base45Encoding.Decode(contents, ignoreErrors)));
        } else if (action == "--encode-file" || action == "-w") {
          if (!File.Exists(inputStr)) {
            Console.Error.WriteLine("File " + inputStr + " does not exist");
            return -3;
          }
          var contents = File.ReadAllText(inputStr);
          Console.Write(Base45Encoding.Encode(Encoding.ASCII.GetBytes(contents)));
        } else {
          Console.Error.WriteLine("Invalid action: " + action);
          return -2;
        }
      } catch (Exception ex) {
        Console.Error.WriteLine("An error occurred: " + ex.Message);
        return -4;
      }
      
      return 0;
    }
  }
  
  // Base45Encoding class adapted and backported to .NET Framework from here:
  // https://github.com/ehn-dcc-development/base45-cs/blob/2072e77db0dfd57a8b0f29b052f2c6d88813f8ad/Base45/Base45Encoding.cs
  
  // Copyright 2021 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
  // Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
  // SPDX-License-Identifier: EUPL-1.2
  
  /// <summary>
  /// https://tools.ietf.org/html/draft-faltstrom-baseBaseSize-01
  /// TL/DR:
  /// This encoding takes a byte array, splits it into 2 byte chunks and encodes each chunk as 3 characters.
  /// Any remaining byte is encoded as 2 characters, padded with a '0' when the remaining byte has value &lt; 45.
  /// </summary>
  public static class Base45Encoding {
    private const int BaseSize = 45;
    private const int BaseSizeSquared = 2025;
    private const int ChunkSize = 2;
    private const int EncodedChunkSize = 3;
    private const int SmallEncodedChunkSize = 2;
    private const int ByteSize = 256;

    private static readonly char[] _Encoding = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                                                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 
                                                'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 
                                                'U', 'V', 'W', 'X', 'Y', 'Z', ' ', '$', '%', '*', 
                                                '+', '-', '.', '/', ':' };

    private static readonly Dictionary<char, byte> _Decoding = new(BaseSize);

    static Base45Encoding() {
      for(byte i = 0; i < _Encoding.Length; ++i)
        _Decoding.Add(_Encoding[i], i);
    }

    public static string Encode(byte[] buffer) {
      if (buffer == null)
        throw new ArgumentNullException(nameof(buffer));

      var wholeChunkCount = buffer.Length / ChunkSize;
      var result = new char[wholeChunkCount * EncodedChunkSize + (buffer.Length % ChunkSize == 1 ? SmallEncodedChunkSize : 0)];

      if (result.Length == 0)
        return string.Empty;

      var resultIndex = 0;
      var wholeChunkLength = wholeChunkCount * ChunkSize;
      for (var i = 0; i < wholeChunkLength;) {
        var value = buffer[i++] * ByteSize + buffer[i++];
        result[resultIndex++] = _Encoding[value % BaseSize];
        result[resultIndex++] = _Encoding[value / BaseSize % BaseSize];
        result[resultIndex++] = _Encoding[value / BaseSizeSquared % BaseSize];
      }

      if (buffer.Length % ChunkSize == 0)
        return new string(result);

      result[result.Length - 2] = _Encoding[buffer[buffer.Length - 1] % BaseSize];
      result[result.Length - 1] = buffer[buffer.Length - 1] < BaseSize ? _Encoding[0] : _Encoding[buffer[buffer.Length - 1] / BaseSize % BaseSize]; 

      return new string(result);
    }

    public static byte[] Decode(string value, bool ignoreErrors = false) {
      if (value == null)
        throw new ArgumentNullException(nameof(value));

      if (value.Length == 0)
        return Array.Empty<byte>();

      var remainderSize = value.Length % EncodedChunkSize;
      if (remainderSize == 1 && !ignoreErrors)
        throw new FormatException("Incorrect length.");

      var buffer = new byte[value.Length];
      for (var i = 0; i < value.Length; ++i) {
        if (_Decoding.TryGetValue(value[i], out var decoded)) {
          buffer[i] = decoded;
          continue; //Earliest return on expected path.
        }

        if (!ignoreErrors)
          throw new FormatException($"Invalid character at position {i}.");
      }

      var wholeChunkCount = buffer.Length / EncodedChunkSize;
      var result = new byte[wholeChunkCount * ChunkSize + (remainderSize == ChunkSize ? 1 : 0)];
      var resultIndex = 0;
      var wholeChunkLength = wholeChunkCount * EncodedChunkSize;
      for (var i = 0;  i < wholeChunkLength; ) {
        var val = buffer[i++] + BaseSize * buffer[i++] + BaseSizeSquared * buffer[i++];
        result[resultIndex++] = (byte)(val / ByteSize); //result is always in the range 0-255 - % ByteSize omitted.
        result[resultIndex++] = (byte)(val % ByteSize); 
      }

      if (remainderSize == 0) 
        return result;
      
      result[result.Length - 1] = (byte)(buffer[buffer.Length - 2] + BaseSize * buffer[buffer.Length - 1]); //result is always in the range 0-255 - % ByteSize omitted.
      return result;
    }
  }
}
