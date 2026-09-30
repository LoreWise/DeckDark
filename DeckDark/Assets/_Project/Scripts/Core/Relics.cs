namespace DeckDark.Core
{
    public enum RelicId { PetDie, BoneAmulet, RabbitFoot, Whetstone, IronFlask, LuckyCoin, RustyHook }

    public class RelicDef
    {
        public RelicId Id;
        public string Name;
        public string Description;
        public int ArmorClassBonus;
    }

    public static class RelicLibrary
    {
        public static readonly RelicDef PetDie = new RelicDef
        {
            Id = RelicId.PetDie, Name = "PET DIE",
            Description = "YOUR FIRST ATTACK IN EVERY COMBAT HAS ADVANTAGE."
        };

        public static readonly RelicDef BoneAmulet = new RelicDef
        {
            Id = RelicId.BoneAmulet, Name = "BONE AMULET",
            Description = "+1 TO YOUR ARMOR CLASS.", ArmorClassBonus = 1
        };

        public static readonly RelicDef RabbitFoot = new RelicDef
        {
            Id = RelicId.RabbitFoot, Name = "RABBIT'S FOOT",
            Description = "HEAL 5 HP AFTER WINNING A COMBAT."
        };

        public static readonly RelicDef Whetstone = new RelicDef
        {
            Id = RelicId.Whetstone, Name = "WHETSTONE",
            Description = "YOUR ATTACKS DEAL +1 DAMAGE."
        };

        public static readonly RelicDef IronFlask = new RelicDef
        {
            Id = RelicId.IronFlask, Name = "IRON FLASK",
            Description = "START EVERY COMBAT WITH 6 BLOCK."
        };

        public static readonly RelicDef LuckyCoin = new RelicDef
        {
            Id = RelicId.LuckyCoin, Name = "LUCKY COIN",
            Description = "YOUR FIRST MISSED ATTACK IN EVERY COMBAT IS REROLLED."
        };

        public static readonly RelicDef RustyHook = new RelicDef
        {
            Id = RelicId.RustyHook, Name = "RUSTY HOOK",
            Description = "YOUR ATTACKS THAT HIT ALSO APPLY 1 BLEED."
        };

        public static readonly RelicDef[] All = { PetDie, BoneAmulet, RabbitFoot, Whetstone, IronFlask, LuckyCoin, RustyHook };
    }
}
