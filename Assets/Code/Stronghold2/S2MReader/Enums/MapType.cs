using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.Stronghold2.S2MReader.Enums
{
  /// <summary>
  /// Map kinds. The numeric values are this enum's own (declaration order); the
  /// map file encodes its kind separately — as the "type" string option in the
  /// file header (see S2MReader.ReadHeader) and as an int in the MapHeader object
  /// (see MapHeaderReader) where kingmaker=0, historical=1, warcampaign=2,
  /// peacecampaign=3, freebuild=4.
  /// </summary>
  public enum MapType
  {
    Kingmaker,
    WarCampaign,
    PeaceCampaign,
    FreeBuild,
    /// <summary>
    /// The .s2c "historical castle" maps (file-header type string "historical").
    /// </summary>
    Historical,
  }
}
