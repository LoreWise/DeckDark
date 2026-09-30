namespace DeckDark.View
{
    /// <summary>
    /// Sprites do prototipo, desenhados como texto. Cada letra e uma cor; '.' e transparente.
    /// Servem de placeholder ate existir arte feita no LibreSprite/Pixelorama.
    /// </summary>
    public static class Sprites
    {
        public static readonly string[] Knight =
        {
            "......kkkk......",
            ".....kssssk.....",
            ".....ksddsk.....",
            ".....kssssk.....",
            "......kssk......",
            "....kkrrrrkk....",
            "...ksrrrrrrsk.w.",
            "...ksrryyrrsk.w.",
            "...ksrrrrrrsk.w.",
            "...kskrrrrksk.w.",
            "....k.rrrr.k..w.",
            "......rrrr...kgk",
            "......ssss....h.",
            "......s..s......",
            ".....kk..kk.....",
            "...bbbbbbbbbb...",
            "..bbbbbbbbbbbb..",
            "...bbbbbbbbbb...",
        };

        public static readonly string[] Goblin =
        {
            "................",
            "...k........k...",
            "...gk.kkkk.kg...",
            "....gkggggkg....",
            ".....gyggyg.....",
            ".....gggggg.....",
            "......gttg......",
            ".....krrrrk.....",
            "....grrrrrrg.w..",
            "....g.rrrr.g.w..",
            "......rrrr...w..",
            "......g..g..kh..",
            ".....kk..kk.....",
            "...bbbbbbbbbb...",
            "..bbbbbbbbbbbb..",
            "...bbbbbbbbbb...",
        };

        public static readonly string[] Skeleton =
        {
            "......wwww......",
            ".....wwwwww.....",
            ".....wkwwkw.....",
            ".....wwwwww.....",
            "......wkkw......",
            "......wwww......",
            "....w.wkkw.w....",
            "....w.wwww.w..r.",
            "....w.wkkw.w..r.",
            ".......ww.....r.",
            "......wkkw...kh.",
            "......w..w......",
            "......w..w......",
            ".....ww..ww.....",
            "...bbbbbbbbbb...",
            "..bbbbbbbbbbbb..",
            "...bbbbbbbbbb...",
        };

        public static readonly string[] Cultist =
        {
            "......pppp......",
            ".....pppppp.....",
            "....ppmmmmpp....",
            "....pmkmmkmp....",
            "....pmmmmmmp....",
            "....ppmmkmpp....",
            "...pppppppppp...",
            "...ppppyypppp...",
            "..pppppyyppppp..",
            "..ppppppppppp.d.",
            "...pppppppppp.d.",
            "...pppppppppp...",
            "....pppppppp....",
            "....pppppppp....",
            "...bbbbbbbbbb...",
            "..bbbbbbbbbbbb..",
            "...bbbbbbbbbb...",
        };

        public static readonly string[] Boss =
        {
            ".....y.y.y.y.y......",
            ".....yyyyyyyyy......",
            ".....yrryyyrry......",
            "......mmmmmmm.......",
            ".....mmmmmmmmm......",
            ".....mmmmmmmmm......",
            ".....mmmmmmmmm......",
            ".....mmmmmmmmm......",
            "......mmmmmmm.......",
            "....cccc.mm.cccc....",
            "...cccccccccccccc...",
            "..cccccccccccccccc..",
            "..cc.ccccyycccc.cc..",
            "..cc.cccccccccc.cc..",
            "..mm.cccccccccc.mm..",
            ".....cccccccccc.....",
            ".....cccccccccc.....",
            ".....ccccc.cccc.....",
            ".....ccc....ccc.....",
            "....kkkk....kkkk....",
            "..bbbbbbbbbbbbbbbb..",
            ".bbbbbbbbbbbbbbbbbb.",
            "..bbbbbbbbbbbbbbbb..",
        };

        // Icones 9x9: 'a' cor principal, 'b' contorno/escuro, 'c' brilho
        public static readonly string[] IconSword =
        {
            ".......cc", "......cac", ".....cac.", "....cac..", ".b.cac...", "..bac....", "..bb.....", ".b..b....", "b........",
        };
        public static readonly string[] IconShield =
        {
            ".bbbbbbb.", "baaaaaaab", "bacaaaaab", "bacaaaaab", "baaaaaaab", ".baaaaab.", ".baaaaab.", "..baaab..", "...bbb...",
        };
        public static readonly string[] IconStar =
        {
            "....a....", "....a....", "...aca...", "aaaacaaaa", ".aaacaaa.", "..aaaaa..", "..aa.aa..", ".aa...aa.", ".a.....a.",
        };
        public static readonly string[] IconDagger =
        {
            ".........", "......cc.", ".....cac.", "....cac..", "...cac...", "..bca....", ".bb......", "b.b......", ".........",
        };
        public static readonly string[] IconHeart =
        {
            ".........", ".aa...aa.", "acaa.aaaa", "acaaaaaaa", "aaaaaaaaa", ".aaaaaaa.", "..aaaaa..", "...aaa...", "....a....",
        };
        public static readonly string[] IconEye =
        {
            ".........", ".........", "..bbbbb..", ".baaaaab.", "baabbbaab", "baabcbaab", ".baaaaab.", "..bbbbb..", ".........",
        };
        public static readonly string[] IconPotion =
        {
            "...bbb...", "...cac...", "...bab...", "..baaab..", ".baaaaab.", "bacaaaaab", "baaaaaaab", "baaaaaaab", ".bbbbbbb.",
        };
        public static readonly string[] IconSkull =
        {
            "..aaaaa..", ".aaaaaaa.", "aaaaaaaaa", "abbaaabba", "abbaaabba", "aaaabaaaa", ".aaaaaaa.", "..a.a.a..", "..a.a.a..",
        };
        public static readonly string[] IconBoot =
        {
            "..bbbb...", "..baab...", "..baab...", "..baab...", "..baab...", "..baaabb.", ".baaaaaab", "baaaaaaab", "bbbbbbbbb",
        };
        public static readonly string[] IconChest =
        {
            ".........", ".bbbbbbb.", "baaaaaaab", "bbbbcbbbb", "baaacaaab", "baaaaaaab", "baaaaaaab", "bbbbbbbbb", ".........",
        };
        public static readonly string[] IconMug =
        {
            ".........", ".ccccc...", "baaaaab..", "baaaaabbb", "baaaaab.b", "baaaaabbb", "baaaaab..", ".bbbbb...", ".........",
        };
        public static readonly string[] IconQuestion =
        {
            "..aaaaa..", ".aa...aa.", ".....aa..", "....aa...", "...aa....", "...aa....", ".........", "...aa....", "...aa....",
        };
        public static readonly string[] IconSwords =
        {
            "c.......c", ".c.....c.", "..c...c..", "...c.c...", "....c....", "...c.c...", ".bb...bb.", ".b.....b.", "b.......b",
        };
        public static readonly string[] IconCrown =
        {
            ".........", "a...a...a", "aa.aaa.aa", "aaaaaaaaa", "acaacaaca", "aaaaaaaaa", "aaaaaaaaa", ".........", ".........",
        };
        public static readonly string[] IconDoor =
        {
            "..bbbbb..", ".baaaaab.", "baaaaaaab", "baaaaaaab", "baaaaacab", "baaaaaaab", "baaaaaaab", "baaaaaaab", "bbbbbbbbb",
        };

        public static readonly string[] Rat =
        {
            "..............",
            "..............",
            ".....kk.......",
            "....kggk......",
            "...kggggkkkk..",
            "..kgyggggggkk.",
            ".kggggggggggk.",
            "kttggggggggk..",
            ".kkgggggggk...",
            "...k.k..k.k...",
            "..bbbbbbbbbb..",
            ".bbbbbbbbbbbb.",
            "..bbbbbbbbbb..",
        };

        public static readonly string[] Ogre =
        {
            ".......kkkkkk.........",
            "......kggggggk........",
            ".....kgggggggk........",
            ".....kgyggygggk.......",
            ".....kggggggggk.......",
            "......kgttttgk........",
            "...kkkkggggggkkkk.....",
            "..kggggrrrrrrggggk....",
            ".kggggrrrrrrrrggggk...",
            ".kgg.krrrrrrrrk.ggk.hh",
            ".kgg.krrrrrrrrk.ggkhhh",
            ".kgg.krrrrrrrrk.ggkhh.",
            "..kk.krrrrrrrrk..kkh..",
            ".....kwwwwwwwwk...h...",
            ".....kggk..kggk.......",
            ".....kggk..kggk.......",
            ".....kggk..kggk.......",
            "....kkkkk..kkkkk......",
            "..bbbbbbbbbbbbbbbbbb..",
            ".bbbbbbbbbbbbbbbbbbbb.",
            "..bbbbbbbbbbbbbbbbbb..",
        };

        public static readonly string[] Ghoul =
        {
            ".....kkkkk......",
            "....kppppppk....",
            "...kpyppyppk....",
            "...kppppppk.....",
            "....kptttpk.....",
            ".....kpppk......",
            "...kkppppppkk...",
            "..kpp.pppp.ppk..",
            ".kp..pppppp..pk.",
            "kw...pppppp...wk",
            ".....pp..pp.....",
            "....pp....pp....",
            "....pp....pp....",
            "...kkk....kkk...",
            ".bbbbbbbbbbbbbb.",
            "bbbbbbbbbbbbbbbb",
            ".bbbbbbbbbbbbbb.",
        };

        public static readonly string[] IconBlood =
        {
            "....a....", "....a....", "...aaa...", "..aacaa..", "..acaaa..", ".aaaaaaa.", ".aaaaaaa.", "..aaaaa..", "...bbb...",
        };
        public static readonly string[] IconFist =
        {
            ".........", "..aaaaa..", ".aacacab.", ".aaaaaab.", ".aaaaaab.", ".aaaaab..", "..aaab...", "..aaa....", "..bbb....",
        };
        public static readonly string[] IconMouth =
        {
            ".........", "..bbbbb..", ".baaaaab.", "baccccab.", "ba.....ab", "baccccab.", ".baaaaab.", "..bbbbb..", ".........",
        };
        public static readonly string[] IconDie =
        {
            "bbbbbbbbb", "baaaaaaab", "bacaaacab", "baaaaaaab", "baaacaaab", "baaaaaaab", "bacaaacab", "baaaaaaab", "bbbbbbbbb",
        };
        public static readonly string[] IconFlame =
        {
            "....a....", "...aa....", "...aaa.a.", "..aacaaa.", ".aaccaaa.", ".acccaaa.", ".accccaa.", "..acccaa.", "...aaaa..",
        };
    }
}
