﻿using System.Collections.Generic;

namespace CaveStoryModdingFramework.TSC
{
    public static class CommandList
    {
        /// <summary>
        /// All default TSC commands in the order they appear in the TSC parser
        /// </summary>
        public static readonly IReadOnlyList<Command> Commands = new List<Command>()
        {
            new Command("<END", "END",          TSCCommandDescriptions.END,     CommandProperties.EndsEvent | CommandProperties.ClearsTextbox),
            new Command("<LI+", "LIfe +",       TSCCommandDescriptions.LIPlus,  "Life"),
            new Command("<ML+", "Max Life +",   TSCCommandDescriptions.MLPlus,  "Life"),
            new Command("<AE+", "Arms Energy +",TSCCommandDescriptions.AEPlus),
            new Command("<IT+", "ITem +",       TSCCommandDescriptions.ITPlus,  ArgumentTypes.Item),
            new Command("<IT-", "ITem -",       TSCCommandDescriptions.ITMinus, ArgumentTypes.Item),
            new Command("<EQ+", "EQuip +",      TSCCommandDescriptions.EQPlus,  ArgumentTypes.EquipFlags),
            new Command("<EQ-", "EQuip -",      TSCCommandDescriptions.EQMinus, ArgumentTypes.EquipFlags),
            new Command("<AM+", "ArMs +",       TSCCommandDescriptions.AMPlus,  new Argument(ArgumentTypes.Arms), new Argument("Ammo")),
            new Command("<AM-", "ArMs -",       TSCCommandDescriptions.AMMinus, ArgumentTypes.Arms),
            new Command("<ZAM", "Zero ArMs",    TSCCommandDescriptions.ZAM),
            new Command("<TAM", "Trade ArMs",   TSCCommandDescriptions.TAM,     "Weapon 1", ArgumentTypes.Arms, "Weapon 2", ArgumentTypes.Arms, "Ammo"),
            new Command("<PS+", "Portal Slot +",TSCCommandDescriptions.PSPlus,  new Argument("Slot"), new Argument(ArgumentTypes.Event)),
            new Command("<MP+", "MaP +",        TSCCommandDescriptions.MPPlus,  ArgumentTypes.MapFlags),
            new Command("<UNI", "UNIt",         TSCCommandDescriptions.UNI,     ArgumentTypes.Unit),
            new Command("<STC", "Save Time Counter", TSCCommandDescriptions.STC),
            new Command("<TRA", "TRAnsport",    TSCCommandDescriptions.TRA, CommandProperties.EndsEvent, "Destination", ArgumentTypes.Map, ArgumentTypes.Event, ArgumentTypes.XCoord, ArgumentTypes.YCoord),
            new Command("<MOV", "MOVe",         TSCCommandDescriptions.MOV,     ArgumentTypes.XCoord, ArgumentTypes.YCoord),
            new Command("<HMC", "Hide My Character", TSCCommandDescriptions.HMC),
            new Command("<SMC", "Show My Character", TSCCommandDescriptions.SMC),
            new Command("<FL+", "FLag +",       TSCCommandDescriptions.FLPlus,  ArgumentTypes.NpcFlags),
            new Command("<FL-", "FLag -",       TSCCommandDescriptions.FLMinus, ArgumentTypes.NpcFlags),
            new Command("<SK+", "SKipflag +",   TSCCommandDescriptions.SKPlus,  ArgumentTypes.SkipFlags),
            new Command("<SK-", "Skipflag -",   TSCCommandDescriptions.SKMinus, ArgumentTypes.SkipFlags),
            new Command("<KEY", "KEY lock",     TSCCommandDescriptions.KEY),
            new Command("<PRI", "PRevent Interaction", TSCCommandDescriptions.PRI),
            new Command("<FRE", "FREe",         TSCCommandDescriptions.FRE),
            new Command("<NOD", "NOD",          TSCCommandDescriptions.NOD),
            new Command("<CLR", "CLeaR",        TSCCommandDescriptions.CLR, CommandProperties.ClearsTextbox),
            new Command("<MSG", "MeSsaGe",      TSCCommandDescriptions.MSG, CommandProperties.ClearsTextbox),
            new Command("<MS2", "MeSsage 2",    TSCCommandDescriptions.MS2, CommandProperties.ClearsTextbox),
            new Command("<MS3", "MeSsage 3",    TSCCommandDescriptions.MS3, CommandProperties.ClearsTextbox),
            new Command("<WAI", "WAIt",         TSCCommandDescriptions.WAI, "Ticks"),
            new Command("<WAS", "WAit until Standing", TSCCommandDescriptions.WAS),
            new Command("<TUR", "Text UnRead?", TSCCommandDescriptions.TUR),
            new Command("<SAT", "Speed-up All Text", TSCCommandDescriptions.SAT),
            new Command("<CAT", "(C?) All Text",TSCCommandDescriptions.CAT),
            new Command("<CLO", "CLOse",        TSCCommandDescriptions.CLO, CommandProperties.ClearsTextbox),
            new Command("<EVE", "EVEnt",        TSCCommandDescriptions.EVE, CommandProperties.EndsEvent, ArgumentTypes.Event),
            new Command("<YNJ", "Yes/No Jump",  TSCCommandDescriptions.YNJ, ArgumentTypes.Event),
            new Command("<FLJ", "FLag Jump",    TSCCommandDescriptions.FLJ, ArgumentTypes.NpcFlags, ArgumentTypes.Event),
            new Command("<SKJ", "SKipflag Jump",TSCCommandDescriptions.SKJ, ArgumentTypes.SkipFlags, ArgumentTypes.Event),
            new Command("<ITJ", "ITem Jump",    TSCCommandDescriptions.ITJ, ArgumentTypes.Item, ArgumentTypes.Event),
            new Command("<AMJ", "ArMs Jump",    TSCCommandDescriptions.AMJ, ArgumentTypes.Arms, ArgumentTypes.Event),
            new Command("<UNJ", "UNit Jump",    TSCCommandDescriptions.UNJ, ArgumentTypes.Unit, ArgumentTypes.Event),
            new Command("<ECJ", "Event Check Jump", TSCCommandDescriptions.ECJ, ArgumentTypes.EquipFlags, ArgumentTypes.Event),
            new Command("<NCJ", "Npc Check Jump", TSCCommandDescriptions.NCJ, ArgumentTypes.NPCType, ArgumentTypes.Event),
            new Command("<MPJ", "MaP Jump",     TSCCommandDescriptions.MPJ, ArgumentTypes.Event),
            new Command("<SSS", "Start Stream Sound", TSCCommandDescriptions.SSS, "Volume"),
            new Command("<CSS", "Clear Stream Sound", TSCCommandDescriptions.CSS),
            new Command("<SPS", "Start Propeller Sound", TSCCommandDescriptions.SPS),
            new Command("<CPS", "Clear Prop. Sound", TSCCommandDescriptions.CPS),
            new Command("<QUA", "QUAke",        TSCCommandDescriptions.QUA, "Ticks"),
            new Command("<FLA", "FLAsh",        TSCCommandDescriptions.FLA),
            new Command("<FAI", "FAde In",      TSCCommandDescriptions.FAI, ArgumentTypes.Direction),
            new Command("<FAO", "FAde Out",     TSCCommandDescriptions.FAO, ArgumentTypes.Direction),
            new Command("<MNA", "Map NAme",     TSCCommandDescriptions.MNA),
            new Command("<FOM", "Focus On Me",  TSCCommandDescriptions.FOM, "Ticks"),
            new Command("<FON", "Focus On Npc", TSCCommandDescriptions.FON, new Argument(ArgumentTypes.NPCEvent), new Argument("Ticks")),
            new Command("<FOB", "Focus On Boss",TSCCommandDescriptions.FOB, new Argument(ArgumentTypes.NPCEvent), new Argument("Ticks")),
            new Command("<SOU", "SOUnd",        TSCCommandDescriptions.SOU, ArgumentTypes.Sound),
            new Command("<CMU", "Change MUsic", TSCCommandDescriptions.CMU, ArgumentTypes.Music),
            new Command("<FMU", "Fade MUsic",   TSCCommandDescriptions.FMU),
            new Command("<RMU", "Restore MUsic",TSCCommandDescriptions.RMU),
            new Command("<MLP", "Map LooP",     TSCCommandDescriptions.MLP),
            new Command("<SLP", "Show Location Portals", TSCCommandDescriptions.SLP, CommandProperties.EndsEvent),
            new Command("<DNP", "Delete NPc",   TSCCommandDescriptions.DNP, ArgumentTypes.NPCEvent),
            new Command("<DNA", "Delete Npc (All)", TSCCommandDescriptions.DNA, ArgumentTypes.NPCType),
            new Command("<BOA", "BOss Animation", TSCCommandDescriptions.BOA, ArgumentTypes.BOA),
            new Command("<CNP", "Change NPc",   TSCCommandDescriptions.CNP, ArgumentTypes.NPCEvent, ArgumentTypes.NPCType, ArgumentTypes.Direction),
            new Command("<ANP", "Animate NPc",  TSCCommandDescriptions.ANP, ArgumentTypes.NPCEvent, ArgumentTypes.ANP, ArgumentTypes.Direction),
            new Command("<INP", "(Initialize?) NPc", TSCCommandDescriptions.INP, ArgumentTypes.NPCEvent, ArgumentTypes.NPCType, ArgumentTypes.Direction),
            new Command("<SNP", "Set NPc",      TSCCommandDescriptions.SNP, ArgumentTypes.NPCType, ArgumentTypes.XCoord, ArgumentTypes.YCoord, ArgumentTypes.Direction),
            new Command("<MNP", "Move NPc",     TSCCommandDescriptions.MNP, ArgumentTypes.NPCEvent, ArgumentTypes.XCoord, ArgumentTypes.YCoord, ArgumentTypes.Direction),
            new Command("<SMP", "Shift Map Parts", TSCCommandDescriptions.SMP, ArgumentTypes.XCoord, ArgumentTypes.YCoord),
            new Command("<CMP", "Change Map Parts", TSCCommandDescriptions.CMP, ArgumentTypes.XCoord, ArgumentTypes.YCoord, ArgumentTypes.TileIndex),
            new Command("<BSL", "Boss Script Load", TSCCommandDescriptions.BSL, ArgumentTypes.NPCEvent),
            new Command("<MYD", "MY Direction", TSCCommandDescriptions.MYD, ArgumentTypes.Direction),
            new Command("<MYB", "MY Bump",      TSCCommandDescriptions.MYB, ArgumentTypes.Direction),
            new Command("<MM0", "My Motion 0",  TSCCommandDescriptions.MM0),
            new Command("<INI", "INItialize",   TSCCommandDescriptions.INI, CommandProperties.EndsEvent),
            new Command("<SVP", "SaVe Profile", TSCCommandDescriptions.SVP),
            new Command("<LDP", "LoaD Profile", TSCCommandDescriptions.LDP, CommandProperties.EndsEvent),
            new Command("<FAC", "FACe",         TSCCommandDescriptions.FAC, ArgumentTypes.Face),
            //new Command("<FAC", "FACe",         TSCCommandDescriptions.FAC, ArgumentTypes.Face), //Duplicate FAC command
            new Command("<GIT", "Graphic ITem", TSCCommandDescriptions.GIT, ArgumentTypes.ItemGraphic),
            new Command("<NUM", "NUMber",       TSCCommandDescriptions.NUM, ArgumentTypes.Number),
            new Command("<CRE", "CREdits",      TSCCommandDescriptions.CRE),
            new Command("<SIL", "Show ILlustration", TSCCommandDescriptions.SIL, ArgumentTypes.CreditIllustration),
            new Command("<CIL", "Clear ILlustration", TSCCommandDescriptions.CIL),
            new Command("<XX1", "XX1",          TSCCommandDescriptions.XX1, ArgumentTypes.IslandFalling),
            new Command("<ESC", "ESCape",       TSCCommandDescriptions.ESC, CommandProperties.EndsEvent),
        };

        /// <summary>
        /// All commands included in the original TSC+ package
        /// </summary>
        public static readonly IReadOnlyList<Command> TSCPlusCommands = new List<Command>()
        {
            new Command("<LRX", "Left Right X", "Jump to W, X, or Y, if the player moves Left, Right, or Shoots",
                "Left Event", ArgumentTypes.Event, "Right Event", ArgumentTypes.Event, "Shoot Event", ArgumentTypes.Event),
            new Command("<FNJ", "Flag NotJump", "Jump if X is not set.", ArgumentTypes.NpcFlags, ArgumentTypes.Event),
            new Command("<VAR", "VARiable set", "Puts X into variable W", ArgumentTypes.Number, ArgumentTypes.Number),
            new Command("<VAZ", "VAriable Zero", "Zeros X variables, starting at variable W", ArgumentTypes.Number, ArgumentTypes.Number),
            new Command("<VAO", "VAriable Operation", "Performs operation $ on W using X", ArgumentTypes.Number, ArgumentTypes.Number),
            new Command("<VAJ", "VAriable Jump", "Compare X to W using method Y, if true jump to Z", ArgumentTypes.Number, ArgumentTypes.Number, ArgumentTypes.Number, ArgumentTypes.Event),
            new Command("<RND", "RaNdoM", "Puts random # between W (min) and X (max) into variable Y", ArgumentTypes.Number, ArgumentTypes.Number, ArgumentTypes.Number),
            new Command("<IMG", "tIMaGe", "Will set TimgFILE.bmp over the screen. The \"tag\" for the file name must be exactly 4 characters", ArgumentTypes.ASCII),
            new Command("<PHY", "PHYsics define", "Change physics variables"),
            //new Command("<NAM", "NAMe", "Displays a name", new Argument("Name", 0, ArgumentTypes.ASCII, "$")), //Only included in some versions of TSC+
        };

        //TODO maybe add a list for common custom commands?
        /*
         mod.Commands.Add(new Command("<RNJ", "RaNdom Jump", "Jumps to a random event",
                            "Event count",
                            new RepeatStructure(RepeatTypes.GlobalIndex, 0, ArgumentTypes.Event)));                
         */
    }
}
