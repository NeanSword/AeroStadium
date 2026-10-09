using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    /// <summary>Rental team desk: a paged Pokédex, animated inspection and six unique slots.</summary>
    public sealed class PokemonSelectionView : MonoBehaviour
    {
        const int PageSize = 24;
        static readonly Color Navy = new Color(.025f,.12f,.32f);
        static readonly Color Royal = new Color(.025f,.31f,.76f);
        static readonly Color Pale = new Color(.78f,.89f,1f);
        static readonly Color Ivory = new Color(1f,.97f,.87f);
        static readonly Color Gold = new Color(1f,.80f,.16f);
        static readonly Color Red = new Color(.98f,.22f,.25f);
        static readonly string[] Types = { "", "Grass", "Fire", "Water", "Electric", "Normal", "Bug", "Poison", "Ground", "Flying", "Psychic", "Fighting", "Rock", "Ghost", "Ice", "Dragon", "Fairy", "Steel", "Dark" };
        readonly List<int> team = new List<int>();
        readonly List<SpeciesDefinition> filtered = new List<SpeciesDefinition>();
        readonly List<Button> cards = new List<Button>();
        readonly List<int> visibleSpeciesIds = new List<int>();
        readonly List<MainMenuPanel> surfaces = new List<MainMenuPanel>();
        readonly List<Text> badges = new List<Text>();
        readonly List<Button> slots = new List<Button>();
        readonly List<Text> slotLabels = new List<Text>();
        readonly List<CardMotion> motion = new List<CardMotion>();
        readonly List<RawImage> slotPortraits = new List<RawImage>();
        readonly List<CanvasGroup> slotBalls = new List<CanvasGroup>();
        readonly Dictionary<int,Texture2D> portraits = new Dictionary<int,Texture2D>();
        readonly float[] slotBounce = new float[6];
        RectTransform watermark; float messageAt;
        sealed class CardMotion
        {
            public RectTransform Rect, Sheen; public MainMenuPanel Halo; public CanvasGroup Group;
            public Vector2 Rest; public float Born, Focus; public Color Accent;
        }
        public event Action<int> PartnerAdded;
        Catalog catalog; Font font; RectTransform root, grid, cursor;
        Text pageLabel, typeLabel, nameLabel, typesLabel, statsLabel, movesLabel, message, hints, count;
        InputField search;
        Button previous, next, add, start, back, typeButton;
        Action<int> inspect; Action<TeamMember[]> launch; Action returnAction;
        int page, filter, focused = -1, openedFrame; float opened, lastInspect; int pendingInspect = -1;
        bool controller, launchRequested;
        public int TeamCount => team.Count;
        public int FilteredCount => filtered.Count;
        public int PageCount => (filtered.Count + PageSize - 1) / PageSize;
        public int PageIndex => page;
        public bool CanLaunch => team.Count == 6;
        public Button StartButton => start;
        public IReadOnlyList<Button> Cards => cards;
        public IReadOnlyList<Button> TeamSlots => slots;
        public IReadOnlyList<int> VisibleSpeciesIds => visibleSpeciesIds;
        public InputField SearchField => search;
        public Button AddButton => add;
        public Button PreviousButton => previous;
        public Button NextButton => next;
        public int FocusedSpeciesId => focused;
        public int FocusedSpecies => focused;
        public bool CursorVisible => controller && cursor!=null && cursor.gameObject.activeSelf;
        public string SelectedType => Types[filter];
        public bool OpeningGuardActive => Time.frameCount <= openedFrame + 1 || Time.unscaledTime - opened < .25f;

        public void Build(RectTransform parent, Font uiFont, Catalog data, Texture preview,
            Action<int> onInspect, Action<TeamMember[]> onLaunch, Action onReturn, int[] savedTeam = null)
        {
            if(parent==null || uiFont==null || data==null) throw new ArgumentNullException("Selection needs a parent, font and catalog.");
            if(root!=null) { root.gameObject.SetActive(false); Release(root.gameObject); }
            team.Clear(); filtered.Clear(); cards.Clear(); visibleSpeciesIds.Clear(); surfaces.Clear(); badges.Clear(); slots.Clear(); slotLabels.Clear(); motion.Clear(); slotPortraits.Clear(); slotBalls.Clear();
            for(int i=0;i<6;i++) slotBounce[i]=-100f;
            catalog = data; font = uiFont; inspect = onInspect; launch = onLaunch; returnAction = onReturn;
            page=filter=0; focused=pendingInspect=-1; lastInspect=-1f; launchRequested=false;
            opened = Time.unscaledTime; openedFrame=Time.frameCount; root = Rect(parent,"Équipe",0,0,1600,900);
            var art = Resources.Load<Texture2D>("UI/AeroStadiumMenuBackground");
            Raw(root,"Illustration",0,0,1600,900,art,new Color(.75f,.85f,1f));
            Panel(root,"Lecture",0,0,1600,900,new Color(.02f,.11f,.34f,.71f));
            Panel(root,"Bannière royale",0,0,1600,133,new Color(.02f,.23f,.59f,.94f));
            Panel(root,"Ligne championnat",0,129,1600,4,Gold);
            watermark=Ball(root,"Pokéball du stade",1190,105,390,.09f); watermark.localRotation=Quaternion.Euler(0,0,-18);
            Panel(root,"Plaque Pokédex",28,137,924,534,new Color(.96f,.96f,.87f,.98f));
            Panel(root,"Barre équipe",28,675,924,143,Royal);
            Panel(root,"Capsule ligue",44,22,279,26,Red);
            Ball(root,"Emblème ligue",49,22,27,1f);
            var logo = Resources.Load<Texture2D>("UI/AeroStadiumLogo");
            Raw(root,"Logo",1290,14,256,112,logo,Color.white);
            Label(root,"LE VESTIAIRE DES DRESSEURS",82,22,236,26,12,Color.white);
            var title=Label(root,"CHOISIS TES PARTENAIRES !",42,52,1165,54,43,Ivory); title.fontStyle=FontStyle.Bold;
            var titleOutline=title.gameObject.AddComponent<Outline>(); titleOutline.effectColor=Navy; titleOutline.effectDistance=new Vector2(3,-3);
            Label(root,"Six Poké Balls. Une équipe. À toi de jouer !",45,106,1040,23,17,Pale);
            Label(root,"POKÉDEX  ·  "+catalog.species.Length+" PARTENAIRES",44,147,226,32,15,Navy);
            var field = Rect(root,"Recherche",276,140,245,40);
            var searchSurface=Panel(field,"Fond",0,0,245,40,Color.white); searchSurface.BorderWidth=2; searchSurface.BorderColor=Royal; searchSurface.raycastTarget=true;
            search = field.gameObject.AddComponent<InputField>();
            search.targetGraphic=searchSurface;
            search.textComponent = Label(field,"",12,0,221,40,17,Navy);
            search.placeholder = Label(field,"Nom ou numéro…",12,0,221,40,17,new Color(.29f,.43f,.59f));
            search.characterLimit = 30; search.lineType=InputField.LineType.SingleLine;
            search.onValueChanged.AddListener(_ => ApplyFilter(false));
            typeButton = MakeButton(root,"Tous les types",533,140,181,40,()=> { filter=(filter+1)%Types.Length; ApplyFilter(true); },17);
            typeLabel = typeButton.GetComponentInChildren<Text>();
            previous = MakeButton(root,"‹",730,140,42,40,()=> ChangePage(-1),28);
            pageLabel = Label(root,"",779,140,104,40,16,Navy,TextAnchor.MiddleCenter);
            next = MakeButton(root,"›",890,140,42,40,()=> ChangePage(1),28);
            grid = Rect(root,"Grille",44,193,888,460);
            var sheet=Panel(root,"Fiche",970,140,586,518,new Color(.025f,.17f,.42f,.97f)); sheet.BorderWidth=3; sheet.BorderColor=Gold;
            Panel(root,"Cartouche inspection",984,151,558,87,Royal);
            Ball(root,"Capsule inspection",1488,155,44,.34f);
            Raw(root,"Modèle animé",996,160,534,278,preview,Color.white);
            nameLabel = Label(root,"",996,164,492,42,28,Ivory); nameLabel.fontStyle=FontStyle.Bold;
            var nameOutline=nameLabel.gameObject.AddComponent<Outline>(); nameOutline.effectColor=Navy; nameOutline.effectDistance=new Vector2(2,-2);
            typesLabel = Label(root,"",996,215,506,26,16,Gold);
            add = MakeButton(root,"AJOUTER À L’ÉQUIPE",996,443,534,44,()=> RequestAdd(focused),18);
            statsLabel = Label(root,"",996,499,254,141,17,Pale);
            movesLabel = Label(root,"",1270,499,263,141,16,Pale);
            Label(root,"TON ÉQUIPE  /  LES SIX DE DÉPART",44,674,565,26,18,Ivory);
            count = Label(root,"",660,675,270,25,15,Gold,TextAnchor.MiddleRight);
            Label(root,"LOCATION · NIVEAU 50 · SANS OBJET",986,674,552,25,15,Pale,TextAnchor.MiddleCenter);
            for(int i=0;i<6;i++)
            {
                int slot=i;
                Button button=MakeButton(root,"",44+i*150,705,140,103,()=> { if(!OpeningGuardActive) RemoveAt(slot); },13);
                var label=button.GetComponentInChildren<Text>(); var labelRect=label.rectTransform; labelRect.anchoredPosition=new Vector2(5,-75); labelRect.sizeDelta=new Vector2(130,27);
                slotPortraits.Add(Raw(button.transform,"Partenaire",35,3,70,70,null,Color.white));
                var emptyBall=Ball(button.transform,"Pokéball libre",43,9,55,.30f); slotBalls.Add(emptyBall.GetComponent<CanvasGroup>());
                var slotPill=Panel(button.transform,"Numéro",5,5,25,25,Red); slotPill.Corner=12;
                Label(button.transform,(i+1).ToString(),5,5,25,25,14,Color.white,TextAnchor.MiddleCenter).fontStyle=FontStyle.Bold;
                label.transform.SetAsLastSibling(); slots.Add(button); slotLabels.Add(label);
            }
            Ball(root,"Annonce",44,824,25,1f);
            message=Label(root,"L’aventure commence avec ton premier partenaire !",78,824,854,27,17,Ivory); message.fontStyle=FontStyle.Bold; messageAt=Time.unscaledTime;
            start=MakeButton(root,"",970,710,586,98,Launch,23);
            back=MakeButton(root,"RETOUR",44,841,140,36,()=> { if(!OpeningGuardActive) returnAction?.Invoke(); },15);
            hints=Label(root,"",212,840,1344,40,17,Pale);
            cursor=Rect(root,"Curseur Pokéball",0,0,35,35);
            var ball=cursor.gameObject.AddComponent<MainMenuIcon>(); ball.Kind=MainMenuIcon.Symbol.PokeBall; ball.raycastTarget=false;
            if(savedTeam!=null) foreach(int id in savedTeam) if(team.Count<6 && !team.Contains(id) && HasSpecies(id)) team.Add(id);
            ApplyFilter(true); RefreshTeam(); SetInputPresentation(false,"Clavier / souris","Entrée","Échap");
        }

        public void SetInputPresentation(bool useController,string device,string confirm,string cancel)
        {
            controller=useController;
            if(cursor!=null && !useController) cursor.gameObject.SetActive(false);
            if(hints!=null) hints.text=device+"   ·   <color=#FFD635>[ "+confirm+" ]</color> Ajouter / retirer   ·   <color=#FFACAA>[ "+cancel+" ]</color> Retour   ·   "+(useController?"Stick / D-pad":"Flèches")+" Naviguer";
        }
        public TeamMember[] GetTeam()
        {
            var result=new TeamMember[team.Count];
            for(int i=0;i<team.Count;i++) result[i]=new TeamMember(team[i],"none");
            return result;
        }
        public bool TryAdd(int id)
        {
            if(!HasSpecies(id)) { Notify("Choisis un partenaire !",Gold); return false; }
            if(team.Contains(id)) { Notify("Déjà dans tes six !",Gold); return false; }
            if(team.Count>=6) { Notify("Équipe complète ! Retire un partenaire pour changer.",Gold); return false; }
            catalog.GetSpecies(id); team.Add(id); RefreshTeam();
            Inspect(id); pendingInspect=-1; slotBounce[team.Count-1]=Time.unscaledTime;
            Notify(catalog.GetSpecies(id).name+" te rejoint !",Ivory); PartnerAdded?.Invoke(id);
            if(CanLaunch) Focus(start); return true;
        }
        public void RemoveAt(int index)
        {
            if(index<0 || index>=6) return;
            if(index>=team.Count) return;
            team.RemoveAt(index); RefreshTeam(); Notify("Une place se libère. À toi de choisir !",Ivory);
        }
        public void SetSearch(string value) { search.text=value??""; }
        public bool SetTypeFilter(string value)
        {
            if(string.IsNullOrWhiteSpace(value) || string.Equals(value,"all",StringComparison.OrdinalIgnoreCase)) { filter=0; ApplyFilter(true); return true; }
            for(int i=1;i<Types.Length;i++) if(string.Equals(value,Types[i],StringComparison.OrdinalIgnoreCase) || Normalize(value)==Normalize(TypeName(Types[i])))
            { filter=i; ApplyFilter(true); return true; }
            return false;
        }
        public void ChangePage(int delta)
        {
            page=Mathf.Clamp(page+delta,0,Mathf.Max(0,PageCount-1)); DrawGrid(true);
        }
        void ApplyFilter(bool focusGrid)
        {
            filtered.Clear(); string query=Normalize(search.text);
            int number; bool byNumber=int.TryParse(query.TrimStart('#'),NumberStyles.Integer,CultureInfo.InvariantCulture,out number);
            foreach(var s in catalog.species)
            {
                bool type=filter==0 || Array.IndexOf(s.types,Types[filter])>=0;
                if(type && (query.Length==0 || (byNumber?s.id==number:Normalize(s.name).Contains(query)))) filtered.Add(s);
            }
            filtered.Sort((a,b)=>a.id.CompareTo(b.id)); page=0;
            typeLabel.text=filter==0?"Tous les types":TypeName(Types[filter]); DrawGrid(focusGrid);
        }
        void DrawGrid(bool selectFirst)
        {
            bool editing=EventSystem.current!=null && EventSystem.current.currentSelectedGameObject==search.gameObject;
            for(int i=grid.childCount-1;i>=0;i--) { var child=grid.GetChild(i).gameObject; child.SetActive(false); Release(child); }
            cards.Clear(); visibleSpeciesIds.Clear(); surfaces.Clear(); badges.Clear(); motion.Clear();
            pageLabel.text=PageCount==0?"0 / 0":(page+1)+" / "+PageCount;
            previous.interactable=page>0; next.interactable=page+1<PageCount;
            int first=page*PageSize, end=Mathf.Min(filtered.Count,first+PageSize);
            for(int i=first;i<end;i++)
            {
                var s=filtered[i]; int id=s.id, cell=i-first;
                var rt=Rect(grid,s.name,cell%6*150,cell/6*117,138,105);
                MainMenuPanel surface=rt.gameObject.AddComponent<MainMenuPanel>(); surface.color=Navy; surface.BorderWidth=2;
                var button=rt.gameObject.AddComponent<Button>(); button.targetGraphic=surface; button.transition=Selectable.Transition.None;
                button.onClick.AddListener(()=>RequestAdd(id));
                var feedback=rt.gameObject.AddComponent<PokemonSelectionFocus>(); feedback.Bind(()=>Inspect(id),()=>!controller);
                rt.gameObject.AddComponent<RectMask2D>();
                var halo=Panel(rt,"Halo champion",2,2,134,101,Color.clear); halo.BorderWidth=3;
                var portrait=Portrait(id);
                if(portrait!=null) Raw(rt,"Portrait",33,1,72,72,portrait,Color.white);
                else { var icon=Rect(rt,"Emblème",53,20,32,32).gameObject.AddComponent<MainMenuIcon>(); icon.Kind=MainMenuIcon.Symbol.PokeBall; icon.raycastTarget=false; }
                for(int t=0;t<s.types.Length;t++) { float width=s.types.Length==1?130:64; var ribbon=Panel(rt,"Type "+s.types[t],4+t*66,64,width,16,TypeColor(s.types[t])); ribbon.Corner=4; Label(rt,TypeName(s.types[t]).ToUpperInvariant(),4+t*66,64,width,16,9,Color.white,TextAnchor.MiddleCenter).fontStyle=FontStyle.Bold; }
                Label(rt,s.name,4,82,130,21,14,Ivory,TextAnchor.MiddleCenter).fontStyle=FontStyle.Bold;
                Label(rt,"#"+id.ToString("000"),5,4,43,20,10,Pale);
                var memberPill=Panel(rt,"Badge équipe",108,5,25,25,Red); memberPill.Corner=12;
                badges.Add(Label(rt,"",108,5,25,25,12,Color.white,TextAnchor.MiddleCenter));
                var sheen=Panel(rt,"Reflet",-30,0,16,105,new Color(1,1,1,.1f)); sheen.Corner=0; sheen.rectTransform.localRotation=Quaternion.Euler(0,0,-17);
                var group=rt.gameObject.AddComponent<CanvasGroup>(); group.alpha=0;
                motion.Add(new CardMotion { Rect=rt,Halo=halo,Sheen=sheen.rectTransform,Group=group,Rest=rt.anchoredPosition,Born=Time.unscaledTime+cell*.022f,Accent=TypeColor(s.types[0]) });
                cards.Add(button); visibleSpeciesIds.Add(id); surfaces.Add(surface);
            }
            if(filtered.Count==0) Label(grid,"Aucun partenaire trouvé. Essaie un autre nom !",0,150,888,60,22,Navy,TextAnchor.MiddleCenter);
            LinkNavigation(); RefreshTeam();
            if(cards.Count>0) { if(selectFirst || !editing) Focus(cards[0]); Inspect(filtered[first].id); }
            else { focused=pendingInspect=-1; nameLabel.text="AUCUN RÉSULTAT"; typesLabel.text="Modifie le nom, le numéro ou le type."; statsLabel.text=movesLabel.text=""; RefreshTeam(); if(selectFirst || !editing) Focus(typeButton); }
        }
        void LinkNavigation()
        {
            if(search==null || back==null) return;
            var targets=new List<Selectable> { search,typeButton,previous,next,add,start,back };
            targets.AddRange(cards); targets.AddRange(slots);
            targets.RemoveAll(target=>target==null || !target.IsActive() || !target.IsInteractable());
            foreach(var target in targets) target.navigation=new Navigation { mode=Navigation.Mode.Explicit,
                selectOnLeft=Neighbor(target,targets,Vector2.left), selectOnRight=Neighbor(target,targets,Vector2.right),
                selectOnUp=Neighbor(target,targets,Vector2.up), selectOnDown=Neighbor(target,targets,Vector2.down) };
        }
        Selectable Neighbor(Selectable source,List<Selectable> candidates,Vector2 direction)
        {
            var rect=(RectTransform)source.transform; Vector2 origin=root.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            float best=float.PositiveInfinity; Selectable result=null;
            foreach(var candidate in candidates)
            {
                if(candidate==source) continue;
                var other=(RectTransform)candidate.transform; Vector2 delta=(Vector2)root.InverseTransformPoint(other.TransformPoint(other.rect.center))-origin;
                float forward=Vector2.Dot(delta,direction); if(forward<1f) continue;
                float sideways=Mathf.Abs(delta.x*direction.y-delta.y*direction.x);
                float score=forward+sideways*3f;
                if(score<best) { best=score; result=candidate; }
            }
            return result;
        }
        void Inspect(int id)
        {
            if(focused==id) return; focused=id; pendingInspect=id;
            var s=catalog.GetSpecies(id);
            nameLabel.text=s.name.ToUpperInvariant()+"  <size=17>#"+id.ToString("000")+"</size>";
            typesLabel.text=""; foreach(string type in s.types) typesLabel.text+="<color=#"+ColorUtility.ToHtmlStringRGB(TypeColor(type))+">■ "+TypeName(type).ToUpperInvariant()+"</color>  "; typesLabel.text+=" · "+s.height.ToString("0.0",CultureInfo.GetCultureInfo("fr-FR"))+" m";
            var v=s.stats; statsLabel.text="<color=#FFFFFF>STATISTIQUES DE BASE</color>\nPV  "+v.hp+"       ATT.  "+v.attack+"\nDÉF.  "+v.defense+"       VIT.  "+v.speed+"\nATT. SPÉ.  "+v.specialAttack+"\nDÉF. SPÉ.  "+v.specialDefense;
            movesLabel.text="<color=#FFFFFF>CAPACITÉS</color>";
            foreach(int move in s.moves) movesLabel.text+="\n"+catalog.GetMove(move).name;
            RefreshTeam();
        }
        void RefreshTeam()
        {
            for(int i=0;i<6;i++) { bool filled=i<team.Count; slots[i].interactable=true; slotLabels[i].text=filled?catalog.GetSpecies(team[i]).name:"PLACE LIBRE"; slotPortraits[i].texture=filled?Portrait(team[i]):null; slotPortraits[i].gameObject.SetActive(filled); slotBalls[i].gameObject.SetActive(!filled); }
            count.text=team.Count+" / 6   ·   NIVEAU 50";
            start.interactable=CanLaunch; start.GetComponentInChildren<Text>().text=CanLaunch?"ÉQUIPE PRÊTE  ›  COMBATTRE":"COMPLÈTE TON ÉQUIPE  ·  "+team.Count+" / 6";
            add.interactable=focused>0 && team.Count<6 && !team.Contains(focused);
            add.GetComponentInChildren<Text>().text=focused>0 && team.Contains(focused)?"DÉJÀ DANS TON ÉQUIPE":team.Count>=6?"ÉQUIPE COMPLÈTE":"AJOUTER À L’ÉQUIPE";
            for(int i=0;i<badges.Count;i++) { int slot=team.IndexOf(visibleSpeciesIds[i]); badges[i].text=slot>=0?(slot+1).ToString():""; badges[i].transform.parent.Find("Badge équipe").gameObject.SetActive(slot>=0); }
            LinkNavigation();
            var current=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            var selectable=current!=null?current.GetComponent<Selectable>():null;
            if(selectable!=null && current.transform.IsChildOf(root) && !selectable.IsInteractable())
                Focus(CanLaunch?start:cards.Count>0?cards[0]:back);
        }
        void RequestAdd(int id) { if(!OpeningGuardActive && !launchRequested) TryAdd(id); }
        bool HasSpecies(int id) { if(catalog==null || id<1) return false; foreach(var s in catalog.species) if(s.id==id) return true; return false; }
        static string Normalize(string value)
        {
            var text=new StringBuilder();
            foreach(char c in (value??"").Trim().Normalize(NormalizationForm.FormD))
                if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark) text.Append(char.ToLowerInvariant(c));
            return text.ToString().Normalize(NormalizationForm.FormC);
        }
        void Launch() { if(CanLaunch && !OpeningGuardActive && !launchRequested) { launchRequested=true; launch?.Invoke(GetTeam()); } }
        void Update()
        {
            if(root==null) return;
            if(pendingInspect>0 && Time.unscaledTime-lastInspect>.12f) { lastInspect=Time.unscaledTime; int id=pendingInspect; pendingInspect=-1; inspect?.Invoke(id); }
            GameObject selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            float now=Time.unscaledTime, blend=1f-Mathf.Exp(-Time.unscaledDeltaTime*14f);
            for(int i=0;i<cards.Count;i++)
            {
                bool active=selected==cards[i].gameObject, member=team.Contains(visibleSpeciesIds[i]);
                var m=motion[i]; m.Focus=Mathf.Lerp(m.Focus,active?1f:0f,blend);
                float enter=Mathf.Clamp01((now-m.Born)/.34f); float arrival=1f-Mathf.Pow(1f-enter,3f);
                m.Group.alpha=arrival; m.Rect.anchoredPosition=m.Rest+new Vector2(0,46f*(1f-arrival)+m.Focus*5f);
                float pulse=1f+m.Focus*Mathf.Sin(now*5f)*.018f;
                m.Rect.localScale=Vector3.one*((.79f+.21f*arrival)*(1f+m.Focus*.065f)*pulse);
                var target=active?new Color(.035f,.30f,.62f):member?new Color(.035f,.25f,.38f):Navy;
                surfaces[i].color=Color.Lerp(surfaces[i].color,target,blend);
                surfaces[i].SetBorder(active?Gold:member?Gold:new Color(.24f,.52f,.85f,.8f));
                m.Halo.SetBorder(new Color(1f,.85f,.21f,m.Focus*(.6f+.4f*Mathf.Sin(now*5f))));
                m.Sheen.anchoredPosition=new Vector2(-35+Mathf.Repeat(now*.58f+i*.17f,1f)*220,0);
                m.Sheen.gameObject.SetActive(active);
            }
            for(int i=0;i<slots.Count;i++)
            {
                float age=Mathf.Max(0,now-slotBounce[i]), bounce=Mathf.Exp(-age*4.5f)*Mathf.Sin(age*22f);
                var slot=(RectTransform)slots[i].transform; slot.anchoredPosition=new Vector2(44+i*150,-705+bounce*18f);
                float lift=Mathf.Exp(-age*4.5f)*.16f; bool active=selected==slots[i].gameObject;
                slot.localScale=Vector3.one*(1f+lift+(active?.045f:0));
            }
            float messageAge=Mathf.Clamp01((now-messageAt)/.4f); message.rectTransform.anchoredPosition=new Vector2(78,-824-(1f-messageAge)*15);
            if(watermark!=null) watermark.localRotation=Quaternion.Euler(0,0,-18+Mathf.Sin(now*.45f)*9);
            bool show=controller && selected!=null && selected.transform.IsChildOf(root);
            cursor.gameObject.SetActive(show);
            if(show) { RectTransform target=selected.transform as RectTransform; if(target!=null) { Vector3 p=root.InverseTransformPoint(target.TransformPoint(new Vector3(target.rect.xMin,target.rect.center.y))); cursor.localPosition=p+new Vector3(-30+Mathf.Sin(now*5)*3,0,0); cursor.localRotation=Quaternion.Euler(0,0,Mathf.Sin(now*4)*12); } }
        }
        void Notify(string text,Color color) { message.text=text; message.color=color; messageAt=Time.unscaledTime; }
        Texture2D Portrait(int id)
        {
            Texture2D texture; if(!portraits.TryGetValue(id,out texture)) { texture=Resources.Load<Texture2D>("UI/PokemonPortraits/"+id.ToString("000")); portraits.Add(id,texture); }
            return texture;
        }
        static Color TypeColor(string type)
        {
            switch(type) { case "Grass":return new Color(.20f,.64f,.28f); case "Fire":return new Color(.95f,.32f,.13f); case "Water":return new Color(.13f,.52f,.93f); case "Electric":return new Color(.86f,.62f,.04f); case "Poison":return new Color(.63f,.24f,.70f); case "Flying":return new Color(.47f,.45f,.83f); case "Bug":return new Color(.48f,.62f,.12f); case "Ground":return new Color(.66f,.43f,.22f); case "Psychic":return new Color(.90f,.26f,.48f); case "Fighting":return new Color(.72f,.24f,.19f); case "Rock":return new Color(.56f,.49f,.30f); case "Ghost":return new Color(.36f,.29f,.66f); case "Ice":return new Color(.13f,.65f,.72f); case "Dragon":return new Color(.31f,.27f,.79f); case "Fairy":return new Color(.77f,.33f,.62f); case "Steel":return new Color(.36f,.51f,.59f); case "Dark":return new Color(.31f,.26f,.32f); default:return new Color(.48f,.48f,.43f); }
        }
        RectTransform Ball(Transform parent,string name,float x,float y,float size,float opacity)
        {
            var r=Rect(parent,name,x,y,size,size); var icon=r.gameObject.AddComponent<MainMenuIcon>(); icon.Kind=MainMenuIcon.Symbol.PokeBall; icon.raycastTarget=false;
            r.gameObject.AddComponent<CanvasGroup>().alpha=opacity; return r;
        }
        static void Focus(Button b) { if(b!=null && b.IsActive() && b.IsInteractable() && EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(b.gameObject); }
        static void Release(GameObject item) { if(Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        { var r=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>(); r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r; }
        MainMenuPanel Panel(Transform parent,string name,float x,float y,float w,float h,Color c)
        { var p=Rect(parent,name,x,y,w,h).gameObject.AddComponent<MainMenuPanel>(); p.color=c;p.raycastTarget=false;return p; }
        RawImage Raw(Transform parent,string name,float x,float y,float w,float h,Texture texture,Color tint)
        { var r=Rect(parent,name,x,y,w,h).gameObject.AddComponent<RawImage>();r.texture=texture;r.color=tint;r.raycastTarget=false;return r; }
        Text Label(Transform parent,string text,float x,float y,float w,float h,int size,Color color,TextAnchor align=TextAnchor.MiddleLeft)
        { var t=Rect(parent,"Texte",x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;t.supportRichText=true;return t; }
        Button MakeButton(Transform parent,string text,float x,float y,float w,float h,Action action,int size)
        { var r=Rect(parent,text,x,y,w,h);var panel=r.gameObject.AddComponent<MainMenuPanel>();panel.color=Royal;panel.Corner=20;panel.BorderWidth=2;panel.BorderColor=Ivory;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=panel;var colors=b.colors;colors.selectedColor=new Color(1f,.90f,.56f);colors.highlightedColor=colors.selectedColor;colors.disabledColor=new Color(.44f,.55f,.70f,.82f);b.colors=colors;b.onClick.AddListener(()=>action());Label(r,text,8,2,w-16,h-4,size,Ivory,TextAnchor.MiddleCenter).fontStyle=FontStyle.Bold;if(w>170) Ball(r,"Capsule",12,(h-27)/2,27,1);return b; }
        static string TypeName(string t)
        { switch(t) { case "Grass":return "Plante";case "Fire":return "Feu";case "Water":return "Eau";case "Electric":return "Électrik";case "Bug":return "Insecte";case "Poison":return "Poison";case "Ground":return "Sol";case "Flying":return "Vol";case "Psychic":return "Psy";case "Fighting":return "Combat";case "Rock":return "Roche";case "Ghost":return "Spectre";case "Ice":return "Glace";case "Dragon":return "Dragon";case "Fairy":return "Fée";case "Steel":return "Acier";case "Dark":return "Ténèbres";default:return "Normal"; } }
    }
    sealed class PokemonSelectionFocus : MonoBehaviour,ISelectHandler,IPointerEnterHandler
    {
        Action focus;Func<bool> pointerAllowed;public void Bind(Action action,Func<bool> allowPointer) { focus=action;pointerAllowed=allowPointer; }
        public void OnSelect(BaseEventData data) { focus?.Invoke(); }
        public void OnPointerEnter(PointerEventData data) { if(pointerAllowed!=null && !pointerAllowed()) return;if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(gameObject);focus?.Invoke(); }
    }
}
