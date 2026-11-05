using System;
using System.Collections.Generic;
using Airships.Core;
using Airships.Game;
using NUnit.Framework;
using UnityEngine;

namespace Airships.Tests
{
    // Deck battles that run on the music's beat, with a three-skill action pad.
    public class BeatBattleTests
    {
        GameContent content;

        [SetUp]
        public void Load()
        {
            content = JsonUtility.FromJson<GameContent>(Resources.Load<TextAsset>("game_content").text);
            content.campaign = JsonUtility.FromJson<CampaignDef>(Resources.Load<TextAsset>("campaign_content").text);
        }

        BattleBeat Beat => BattleBeat.From(content.timing);

        [Test]
        public void ASequenceIsThreeSecondsOfEightBeats()
        {
            Assert.AreEqual(3.0, Beat.SequenceSeconds, 1e-6);
            Assert.AreEqual(0.375, Beat.BeatSeconds, 1e-6);
        }

        [Test]
        public void TurnsWaitForTheNextSequenceButNotForOneJustMissed()
        {
            var beat = Beat;
            Assert.AreEqual(0.0, beat.NextSequence(0.0), 1e-9, "The song's first beat opens a turn");
            Assert.AreEqual(0.0, beat.NextSequence(0.05), 1e-9, "A frame late still counts as on the beat");
            Assert.AreEqual(3.0, beat.NextSequence(0.4), 1e-9);
            Assert.AreEqual(6.0, beat.NextSequence(5.99), 1e-9);
            Assert.AreEqual(1.5, beat.Next(0.2, 4), 1e-9, "Enemies wait for the next bar");
        }

        [Test]
        public void AChosenActionStartsOnTheNextBeatAhead()
        {
            var beat = Beat;
            Assert.AreEqual(0.375, beat.NextAhead(0.01), 1e-9);
            Assert.AreEqual(0.75, beat.NextAhead(0.375), 1e-9, "Never a beat that has already gone");
            Assert.AreEqual(3, beat.BeatInSequence(1.2));
            Assert.AreEqual(0, beat.BeatInSequence(3.01));
            Assert.That(beat.Pulse(0.0), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void EveryTimingPatternLandsOnTheMusicAndFitsInASequence()
        {
            var beat = Beat;
            foreach (var pattern in content.timing.patterns)
            {
                Assert.IsTrue(beat.OnGrid(pattern.beats[0].at, 1), $"{pattern.id} opens on a whole beat");
                foreach (var step in pattern.beats)
                {
                    Assert.IsTrue(beat.OnGrid(step.at, 0.5), $"{pattern.id} step at {step.at} is off the eighth-note grid");
                    if (step.IsDrag) Assert.IsTrue(beat.OnGrid(step.End, 0.5), $"{pattern.id} drag ends off the grid");
                }
                Assert.Less(pattern.Length, beat.SequenceSeconds, $"{pattern.id} outlasts a sequence");
            }
        }

        [Test]
        public void EveryCrewMemberHasThreeSkillsWithTheirOwnIcons()
        {
            foreach (var member in content.crew)
            {
                Assert.NotNull(member.skills, member.id);
                Assert.AreEqual(BattleSetup.SkillSlots, member.skills.Length, $"{member.id} needs exactly three skills");
                var seen = new HashSet<string>();
                foreach (var unlock in member.skills)
                {
                    var skill = content.Skill(unlock.skill);
                    Assert.IsTrue(seen.Add(skill.id), $"{member.id} lists {skill.id} twice");
                    Assert.IsFalse(string.IsNullOrEmpty(skill.icon), $"{skill.id} has no icon");
                    Assert.NotNull(Resources.Load<Sprite>($"Art/Icons/T_Icon_{skill.icon}_HQ"), $"{skill.icon} HQ");
                    Assert.NotNull(Resources.Load<Sprite>($"Art/Icons/T_Icon_{skill.icon}_LQ"), $"{skill.icon} LQ");
                    Assert.DoesNotThrow(() => { _ = skill.Kind; _ = skill.Target; }, skill.id);
                }
            }

            // Skills sharing a picture would make two buttons look the same.
            var owners = new Dictionary<string, string>();
            foreach (var skill in content.skills)
            {
                if (string.IsNullOrEmpty(skill.icon)) continue;
                Assert.IsFalse(owners.TryGetValue(skill.icon, out var other), $"{skill.id} and {other} share {skill.icon}");
                owners[skill.icon] = skill.id;
            }
            Assert.NotNull(Resources.Load<Sprite>("Art/Icons/T_Icon_Hud_Attack_HQ"));
            Assert.NotNull(Resources.Load<Sprite>("Art/Icons/T_Icon_Item_Rum_HQ"));
        }

        [Test]
        public void RookFightsWithCinderAndNeverMoreThanThreeSkills()
        {
            var rook = BattleSetup.CreateCrew(content, content.Crew("rook"), 10, 0);
            Assert.AreEqual(3, rook.Skills.Count);
            Assert.IsTrue(rook.Skills.Exists(s => s.id == "cinder_flame"));
            Assert.AreEqual("Skill_CinderFlame", rook.Skills.Find(s => s.id == "cinder_flame").icon);

            var newcomer = BattleSetup.CreateCrew(content, content.Crew("lyra"), 1, 0);
            Assert.AreEqual(1, newcomer.Skills.Count, "Skills still open with level");
            foreach (var member in content.crew)
            {
                Assert.LessOrEqual(BattleSetup.CreateCrew(content, member, 99, 0).Skills.Count, BattleSetup.SkillSlots, member.id);
            }
        }

        [Test]
        public void EveryoneStartsWithOneSkillAndLearnsTheirSecondAtFiveAndThirdAtTen()
        {
            foreach (var member in content.crew)
            {
                Assert.AreEqual(3, member.skills.Length, $"{member.id} has three skills to learn");
                Assert.AreEqual(1, BattleSetup.CreateCrew(content, member, 1, 0).Skills.Count, $"{member.id} starts with one");
                Assert.AreEqual(1, BattleSetup.CreateCrew(content, member, 4, 0).Skills.Count, $"{member.id} still has one at level 4");
                Assert.AreEqual(2, BattleSetup.CreateCrew(content, member, 5, 0).Skills.Count, $"{member.id} learns a second at level 5");
                Assert.AreEqual(2, BattleSetup.CreateCrew(content, member, 9, 0).Skills.Count, $"{member.id} still has two at level 9");
                Assert.AreEqual(3, BattleSetup.CreateCrew(content, member, 10, 0).Skills.Count, $"{member.id} learns a third at level 10");
            }
        }

        PartyBattle Deck(out Combatant hero, out Combatant foe)
        {
            hero = BattleSetup.CreateCrew(content, content.Crew("vane"), 3, 0);
            var cls = content.Class("brigand");
            foe = Combatant.Create("foe", "Foe", cls, cls.baseStats, 1, content, false, 0);
            return new PartyBattle(new List<Combatant> { hero }, new List<Combatant> { foe }, 7, content.timing);
        }

        static void WaitForTurn(PartyBattle battle, Combatant actor)
        {
            for (var i = 0; battle.Current != actor; i++)
            {
                Assert.Less(i, 100, $"{actor.Name} never got a turn");
                battle.Submit(battle.Current.IsPlayer ? new BattleAction { Kind = BattleActionKind.Guard } : new BattleAction { Kind = BattleActionKind.Attack, Target = battle.Party[0] });
            }
        }

        [Test]
        public void APerfectBlockCountersWithoutGuarding()
        {
            var battle = Deck(out var hero, out var foe);
            WaitForTurn(battle, foe);
            hero.Guarding = false;
            var hp = foe.Hp;
            var results = battle.Submit(new BattleAction { Kind = BattleActionKind.Attack, Target = hero, Timing = TimingGrade.Perfect });
            Assert.IsTrue(results.Exists(r => r.Kind == BattleEventKind.Counter));
            Assert.Less(foe.Hp, hp);
        }

        [Test]
        public void RunningOutOfTimeLosesTheTurnToTheNextFighter()
        {
            var battle = Deck(out var hero, out var foe);
            WaitForTurn(battle, hero);
            var round = battle.Round;
            var heroHp = hero.Hp;
            var heroMp = hero.Mp;
            var foeHp = foe.Hp;
            var results = battle.Submit(new BattleAction { Kind = BattleActionKind.Pass });
            Assert.IsFalse(results.Exists(r => r.Kind == BattleEventKind.Damage || r.Kind == BattleEventKind.Heal), "A lost turn does nothing");
            Assert.AreEqual(foeHp, foe.Hp);
            Assert.AreEqual(heroHp, hero.Hp);
            Assert.AreEqual(heroMp, hero.Mp);
            Assert.IsFalse(hero.Guarding);
            Assert.IsTrue(battle.Current == foe || battle.Round > round, "The turn passes on to the next fighter");
        }

        [Test]
        public void APlainAttackWinsBackSomeMp()
        {
            var battle = Deck(out var hero, out var foe);
            foe.Hp = 9999;
            foe.Stats.hp = 9999f;
            WaitForTurn(battle, hero);
            hero.Mp = 0;
            battle.Submit(new BattleAction { Kind = BattleActionKind.Attack, Target = foe, Timing = TimingGrade.Good });
            Assert.AreEqual((int)Math.Ceiling(hero.Stats.mp * PartyBattle.AttackMpShare), hero.Mp);
        }

        [Test]
        public void DeckBattlesOnlyPlaySongsOnTheBeat()
        {
            for (var i = 0; i < 4; i++) CollectionAssert.Contains(GameMusic.BeatSongs, GameMusic.For(GameState.Battle, null));
            Assert.AreEqual(GameMusic.TyrantsTempo, GameMusic.DeckBattleSong(new PhaseDef { music = GameMusic.Boss }));
            Assert.AreEqual(GameMusic.TyrantsTempo, GameMusic.DeckBattleSong(new PhaseDef { music = GameMusic.Finale }));
            Assert.AreEqual(GameMusic.Boss, GameMusic.For(GameState.Dogfight, new PhaseDef { music = GameMusic.Boss }), "Air fights keep their own music");
        }

        [Test]
        public void BeatSongsKeepWholeSequencesAcrossTheLoop()
        {
            var sequence = Beat.SequenceSeconds;
            foreach (var song in GameMusic.BeatSongs)
            {
                foreach (var layer in new[] { "base", "danger" })
                {
                    foreach (var part in new[] { "intro", "loop" })
                    {
                        var clip = Resources.Load<AudioClip>($"Music/{song}_{layer}_{part}");
                        Assert.NotNull(clip, $"{song} {layer} {part}");
                        var sequences = clip.length / sequence;
                        Assert.AreEqual(Math.Round(sequences), sequences, 0.002, $"{song} {layer} {part} is {clip.length} s, not whole sequences");
                    }
                }
            }
        }
    }
}
