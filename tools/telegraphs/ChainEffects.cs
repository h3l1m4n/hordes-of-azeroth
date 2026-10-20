using System.IO;

namespace BonkTelegraphs
{
    static class ChainEffects
    {
        const uint LightningChain = 1;
        const int BoltLifeField = 5;
        const int BoltStaggerField = 6;
        const uint BoltLifeMs = 600;
        const uint BoltStaggerMs = 80;

        public static byte[] Quicken(byte[] data)
        {
            if (data == null)
                throw new FileNotFoundException("SpellChainEffects.dbc is not in the client's archives.");
            Dbc chains = new Dbc(data);
            uint[] lightning = chains.Find(LightningChain);
            if (lightning == null)
                throw new InvalidDataException("SpellChainEffects.dbc has no lightning chain row.");
            lightning[BoltLifeField] = BoltLifeMs;
            lightning[BoltStaggerField] = BoltStaggerMs;
            return chains.ToBytes();
        }
    }
}
