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
            Id = RelicId.PetDie, Name = "DADO DE ESTIMAÇÃO",
            Description = "SEU PRIMEIRO ATAQUE EM CADA COMBATE TEM VANTAGEM."
        };

        public static readonly RelicDef BoneAmulet = new RelicDef
        {
            Id = RelicId.BoneAmulet, Name = "AMULETO DE OSSO",
            Description = "+1 NA SUA CLASSE DE ARMADURA.", ArmorClassBonus = 1
        };

        public static readonly RelicDef RabbitFoot = new RelicDef
        {
            Id = RelicId.RabbitFoot, Name = "PÉ DE COELHO",
            Description = "CURA 4 PV AO VENCER UM COMBATE."
        };

        public static readonly RelicDef[] All = { PetDie, BoneAmulet, RabbitFoot };
    }
}
