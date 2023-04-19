﻿using System.Collections.Generic;
using System.Text;

namespace Soundboard4MacroDeck.MimeSniffer;

/// <summary>
/// metadata
/// </summary>
public class Metadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Metadata"/> class.
    /// </summary>
    public Metadata()
    {
        Offsets = new(4);
    }

    /// <summary>
    /// Gets or sets the extension list of this metadata.
    /// </summary>
    public List<string> Extensions { get; set; }

    /// <summary>
    /// Gets or sets the offset list of one metadata.
    /// </summary>
    public List<Offset> Offsets { get; set; }

    /// <summary>
    /// Start match.
    /// </summary>
    /// <param name="data">The hex head of one file.</param>
    /// <returns>match result: matched or not.</returns>
    public bool Match(byte[] data)
    {
        foreach (var offset in Offsets)
        {
            if (data.Length < offset.Start + offset.Count)
            {
                // the data's length less than the length of metadata required.
                return false;
            }
            if (offset.Value == Encoding.ASCII.GetString(data, offset.Start, offset.Count))
            {
                continue;
            }
            return false;
        }
        return true;
    }
}