namespace DeckDark.Core
{
    public enum RelicId { PetDie, BoneAmulet, RabbitFoot }

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
            Description = "HEAL 4 HP AFTER WINNING A COMBAT."
        };

        public static readonly RelicDef[] All = { PetDie, BoneAmulet, RabbitFoot };
    }
}
