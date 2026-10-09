from pathlib import Path
import shutil,uuid
repo=Path(r'C:\Users\dofus\Documents\GitHub\AeroStadium')
backup=repo/'output/mechanics/iv-ev-20261009/before'
if backup.exists():raise SystemExit('Backup exists; inspect before running again')
for rel in ['Assets/AeroStadium/Core/BattleEngine.cs','Tests/CoreChecks/Program.cs','docs/KANTO_BASE_STATS.md']:
 dst=backup/rel;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(repo/rel,dst)
values='''using System;

namespace AeroStadium.Core
{
    /// <summary>Six individual or effort values; zero is an explicit valid value.</summary>
    [Serializable]
    public sealed class StatValues
    {
        public int hp, attack, defense, specialAttack, specialDefense, speed;
        public StatValues() { }
        public StatValues(int value)
        {
            hp=attack=defense=specialAttack=specialDefense=speed=value;
        }
        public StatValues Copy()
        {
            return new StatValues { hp=hp, attack=attack, defense=defense,
                specialAttack=specialAttack, specialDefense=specialDefense, speed=speed };
        }
        internal void Validate(int maximum, int totalLimit, string parameter)
        {
            int[] values={hp,attack,defense,specialAttack,specialDefense,speed};
            int total=0;
            foreach(int value in values) {
                if(value<0||value>maximum)
                    throw new ArgumentOutOfRangeException(parameter,"Each value must be between 0 and "+maximum+".");
                total+=value;
            }
            if(totalLimit>=0&&total>totalLimit)
                throw new ArgumentException("The total cannot exceed "+totalLimit+".",parameter);
        }
    }
}
'''
path=repo/'Assets/AeroStadium/Core/StatValues.cs'
assert not path.exists()
path.write_text(values,encoding='utf-8')
path.with_suffix('.cs.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
engine=repo/'Assets/AeroStadium/Core/BattleEngine.cs'
s=engine.read_text(encoding='utf-8-sig')
def rep(text,old,new):
 assert old in text,old[:80]
 return text.replace(old,new)
s=rep(s,'public string itemId;','public string itemId;\n        // Null preserves the original rental profile: IV31 and EV0 in every stat.\n        public StatValues ivs, evs;')
s=rep(s,'internal ItemDefinition HeldItem;','readonly StatValues individualValues, effortValues;\n        public StatValues IndividualValues { get { return individualValues.Copy(); } }\n        public StatValues EffortValues { get { return effortValues.Copy(); } }\n        internal ItemDefinition HeldItem;')
old='''            BaseStats b = Species.stats;
            MaxHp = ((2 * b.hp + 31) * level / 100) + level + 10;
            Hp = MaxHp;
            Attack = Stat(b.attack, level); Defense = Stat(b.defense, level);
            SpecialAttack = Stat(b.specialAttack, level); SpecialDefense = Stat(b.specialDefense, level);
            Speed = Stat(b.speed, level);'''
new='''            individualValues=(member.ivs??new StatValues(31)).Copy();
            effortValues=(member.evs??new StatValues()).Copy();
            individualValues.Validate(31,-1,nameof(member.ivs));
            effortValues.Validate(252,510,nameof(member.evs));
            BaseStats b = Species.stats;
            MaxHp = ScaledBase(b.hp,individualValues.hp,effortValues.hp,level) + level + 10;
            Hp = MaxHp;
            Attack = Stat(b.attack,individualValues.attack,effortValues.attack,level);
            Defense = Stat(b.defense,individualValues.defense,effortValues.defense,level);
            SpecialAttack = Stat(b.specialAttack,individualValues.specialAttack,effortValues.specialAttack,level);
            SpecialDefense = Stat(b.specialDefense,individualValues.specialDefense,effortValues.specialDefense,level);
            Speed = Stat(b.speed,individualValues.speed,effortValues.speed,level);'''
s=rep(s,old,new)
s=rep(s,'private static int Stat(int basis, int level) { return ((2 * basis + 31) * level / 100) + 5; }','private static int ScaledBase(int basis,int iv,int ev,int level) { return (2*basis+iv+ev/4)*level/100; }\n        private static int Stat(int basis,int iv,int ev,int level) { return ScaledBase(basis,iv,ev,level)+5; }')
engine.write_text(s,encoding='utf-8')
tests=repo/'Tests/CoreChecks/Program.cs'
s=tests.read_text(encoding='utf-8-sig')
s=rep(s,'("Unknown data and unsupported effects fail validation", InvalidCatalog)','("Unknown data and unsupported effects fail validation", InvalidCatalog),\n            ("IV/EV profiles preserve defaults and exact known stats", TrainingStats),\n            ("IV/EV limits and total cap reject invalid teams", TrainingLimits),\n            ("IV/EV profiles are isolated from caller mutations", TrainingIsolation),\n            ("Training changes damage categories and battle initiative", TrainingCombat)')
methods='''    private static BattleEngine Trained(Catalog catalog, StatValues ivs=null, StatValues evs=null)
    {
        return new BattleEngine(catalog,new[] {new TeamMember(1025) {ivs=ivs,evs=evs}},new[] {new TeamMember(4096)},123);
    }
    private static string SixStats(BattlePokemon p)
    {
        return string.Join(",",p.MaxHp,p.Attack,p.Defense,p.SpecialAttack,p.SpecialDefense,p.Speed);
    }
    private static void TrainingStats()
    {
        Catalog c=Fixture();c.species[0].stats=Stats(78,84,78,109,85,100);
        Require(SixStats(Trained(c).Active(0))=="153,104,98,129,105,120","Legacy rental stats changed.");
        Require(SixStats(Trained(c,new StatValues(0)).Active(0))=="138,89,83,114,90,105","Explicit zero IVs were replaced by defaults.");
        Require(SixStats(Trained(c,null,new StatValues {hp=4,specialAttack=252,speed=252}).Active(0))=="154,104,98,161,105,152","Known level50 trained Charizard stats differ.");
        var a=new BattleEngine(c,new[]{new TeamMember(1025) {evs=new StatValues {attack=3}}},new[]{new TeamMember(4096)},1,100);
        var b=new BattleEngine(c,new[]{new TeamMember(1025) {evs=new StatValues {attack=4}}},new[]{new TeamMember(4096)},1,100);
        Require(a.Active(0).Attack==204&&b.Active(0).Attack==205,"EV remainder was used as a fractional point.");
    }
    private static void TrainingLimits()
    {
        foreach(int bad in new[]{-1,32,int.MaxValue})Throws(()=>Trained(Fixture(),new StatValues {speed=bad}));
        foreach(int bad in new[]{-1,253,int.MaxValue})Throws(()=>Trained(Fixture(),null,new StatValues {hp=bad}));
        Trained(Fixture(),null,new StatValues {attack=252,speed=252,hp=6});
        Throws(()=>Trained(Fixture(),null,new StatValues {attack=252,speed=252,hp=7}));
        string[] fields={"hp","attack","defense","specialAttack","specialDefense","speed"};
        foreach(string field in fields) {
            var ivs=new StatValues(31);typeof(StatValues).GetField(field).SetValue(ivs,32);
            Throws(()=>Trained(Fixture(),ivs));
            var evs=new StatValues();typeof(StatValues).GetField(field).SetValue(evs,253);
            Throws(()=>Trained(Fixture(),null,evs));
        }
    }
    private static void TrainingIsolation()
    {
        var ivs=new StatValues(31);var evs=new StatValues {attack=252};
        var shared=new TeamMember(1025) {ivs=ivs,evs=evs};
        var e=new BattleEngine(Fixture(),new[]{shared},new[]{shared},1);
        ivs.attack=0;evs.attack=0;e.Active(0).IndividualValues.speed=0;e.Active(0).EffortValues.attack=0;
        Require(e.Active(0).IndividualValues.attack==31&&e.Active(1).IndividualValues.attack==31,"Caller IV mutation leaked into a fighter.");
        Require(e.Active(0).EffortValues.attack==252&&e.Active(1).EffortValues.attack==252,"EV profiles were shared.");
        var options=new JsonSerializerOptions {IncludeFields=true};
        var restored=JsonSerializer.Deserialize<TeamMember>(JsonSerializer.Serialize(new TeamMember(1025) {ivs=new StatValues(0),evs=new StatValues {speed=252}},options),options);
        Require(Trained(Fixture(),restored.ivs,restored.evs).Active(0).IndividualValues.hp==0,"Team JSON lost explicit zero IVs.");
    }
    private static int TrainingHit(StatValues evs,int slot)
    {
        return Trained(Fixture(),null,evs).ResolveTurn(BattleChoice.Move(slot),BattleChoice.Move(2)).First(e=>e.Kind==BattleEventKind.Damage&&e.TargetSide==1).Amount;
    }
    private static void TrainingCombat()
    {
        Require(TrainingHit(new StatValues {attack=252},0)>TrainingHit(null,0),"Attack EVs did not affect physical damage.");
        Require(TrainingHit(new StatValues {attack=252},1)==TrainingHit(null,1),"Attack EVs leaked into special damage.");
        Require(TrainingHit(new StatValues {specialAttack=252},1)>TrainingHit(null,1),"Special Attack EVs did not affect special damage.");
        Require(TrainingHit(new StatValues {specialAttack=252},0)==TrainingHit(null,0),"Special Attack EVs leaked into physical damage.");
        Catalog c=Fixture();c.species[0].stats.speed=c.species[1].stats.speed=100;
        var slow=Trained(c,new StatValues(0));var fast=Trained(c,new StatValues(0),new StatValues {speed=252});
        Require(slow.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0)).First(e=>e.Kind==BattleEventKind.MoveUsed).Side==1,"Zero Speed IVs should act second.");
        Require(fast.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0)).First(e=>e.Kind==BattleEventKind.MoveUsed).Side==0,"Speed EVs did not change initiative.");
    }
'''
s=rep(s,'    private static void ActualCatalog(string path)',methods+'    private static void ActualCatalog(string path)')
tests.write_text(s,encoding='utf-8')
print('IV/EV model, calculation, validation and combat checks installed; old files backed up.')
