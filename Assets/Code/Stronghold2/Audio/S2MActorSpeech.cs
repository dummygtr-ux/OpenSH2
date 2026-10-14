using System.Collections.Generic;

namespace Assets.Code.Stronghold2.Audio
{
  /// <summary>
  /// Which speech id belongs to which placed-actor type - the alias table the
  /// shipped sheet implies but never states.
  ///
  /// The speech sheet names 20 unit voices, and each one comes as a SELECT /
  /// MOVE pair (FXT_ARCHER_SELECT, FXT_ARCHER_MOVE, ...): the two verbs a
  /// player's own click grammar issues, which is why they are the pair wired
  /// to the commander. The sheet's stems are the game's internal unit names,
  /// which are NOT the roster's type names - the roster comes from the map
  /// data (entity_types.csv, 47 types) and the sheet from the voice recordings,
  /// and they agree only by eye:
  ///
  ///   roster type    sheet stem      why it is not a string rule
  ///   Archer         ARCHER         same
  ///   ArmedPeasant   PEASANT        the roster marks the armed variant
  ///   AxeThrower     AXETHROWER     same
  ///   Berserker      BERSERKER      same
  ///   BoatWarrior    BOAT           the roster names the unit, the sheet the craft
  ///   Crossbowman    CROSSBOWMEN    plural in the sheet
  ///   Engineer       ENGINEER       same
  ///   HorseArcher    HORSEARCHER    the two words are SWAPPED
  ///   Knight         KNIGHT         same
  ///   Ladderman      LADDER         the roster names the man, the sheet the engine
  ///   LightCavalry   CAVALRY        the roster qualifies the troop type
  ///   Maceman        MACEMEN        plural in the sheet
  ///   Monk           MONK           same
  ///   Outlaw         OUTLAW         same
  ///   Pikeman        PIKEMEN        plural in the sheet
  ///   Spearman       SPEARMEN       plural in the sheet
  ///   Swordsman      SWORDSMEN      plural in the sheet
  ///   WarriorMonk    WARRIOR_MONK   the roster's camel case becomes two words
  ///
  /// The pair FXT_THIEF_* / FXT_ASSASSIN_* has no roster type at all - the 47
  /// shipped types hold no Thief and no Assassin - so 18 of the sheet's 20
  /// voices are reachable from the map data, and the other two are recorded
  /// here as unreachable rather than silently dropped. Nothing else on the
  /// roster speaks: a Ballista, a Sheep or a SiegeTower has no id in the
  /// sheet, and <see cref="SelectId"/> answers null for them so the caller
  /// can say WHY nothing played instead of playing something arbitrary.
  ///
  /// Whether a given recording is one voice or several men is the sheet's own
  /// business (each id carries its own variant list); this table only names
  /// the pair.
  /// </summary>
  public static class S2MActorSpeech
  {
    /// <summary>Roster type -> the sheet's stem for that unit's voice.</summary>
    private static readonly Dictionary<string, string> Stems =
      new Dictionary<string, string>
      {
        { "Archer", "ARCHER" },
        { "ArmedPeasant", "PEASANT" },
        { "AxeThrower", "AXETHROWER" },
        { "Berserker", "BERSERKER" },
        { "BoatWarrior", "BOAT" },
        { "Crossbowman", "CROSSBOWMEN" },
        { "Engineer", "ENGINEER" },
        { "HorseArcher", "HORSEARCHER" },
        { "Knight", "KNIGHT" },
        { "Ladderman", "LADDER" },
        { "LightCavalry", "CAVALRY" },
        { "Maceman", "MACEMEN" },
        { "Monk", "MONK" },
        { "Outlaw", "OUTLAW" },
        { "Pikeman", "PIKEMEN" },
        { "Spearman", "SPEARMEN" },
        { "Swordsman", "SWORDSMEN" },
        { "WarriorMonk", "WARRIOR_MONK" },
      };

    /// <summary>The sheet ids no roster type can reach (no such unit ships).</summary>
    public static readonly string[] UnreachableIds =
      { "FXT_THIEF_SELECT", "FXT_THIEF_MOVE", "FXT_ASSASSIN_SELECT", "FXT_ASSASSIN_MOVE" };

    /// <summary>How many roster types this table covers.</summary>
    public static int MappedTypeCount => Stems.Count;

    /// <summary>The select voice for a roster type, or null when it has none.</summary>
    public static string SelectId(string type) => Id(type, "SELECT");

    /// <summary>The move voice for a roster type, or null when it has none.</summary>
    public static string MoveId(string type) => Id(type, "MOVE");

    /// <summary>A named reason why a type has no voice, for the caller's log.</summary>
    public static string WhySilent(string type)
    {
      if (string.IsNullOrEmpty(type)) return "the actor carries no type name";
      return Stems.ContainsKey(type)
        ? $"'{type}' has a voice id but the catalogue did not resolve it"
        : $"'{type}' has no SELECT/MOVE voice in fx/sh2_speech.csv "
          + $"({Stems.Count} of the 47 roster types do)";
    }

    private static string Id(string type, string verb)
    {
      if (string.IsNullOrEmpty(type) || !Stems.TryGetValue(type, out string stem)) return null;
      return $"FXT_{stem}_{verb}";
    }
  }
}
