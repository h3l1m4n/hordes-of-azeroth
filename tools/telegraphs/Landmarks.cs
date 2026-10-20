using System;
using System.IO;

namespace BonkTelegraphs
{
    static class Landmarks
    {
        const int FieldCount = 29;
        const int FlagsColumn = 8;
        const uint MinimapCandidate = 0x1;

        public static int HideFromMinimap(Dbc areaPois)
        {
            if (areaPois.FieldCount != FieldCount)
                throw new InvalidDataException("AreaPOI.dbc has " + areaPois.FieldCount + " columns, not the 1.12 client's " + FieldCount + ".");
            int hidden = 0;
            foreach (uint[] row in areaPois.Records)
                if ((row[FlagsColumn] & MinimapCandidate) != 0)
                {
                    row[FlagsColumn] &= ~MinimapCandidate;
                    hidden++;
                }
            return hidden;
        }
    }
}
