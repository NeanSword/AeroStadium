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
    /// <summary>Detached team-member workshop. Only Save commits the validated draft.</summary>
    public sealed class PokemonLoadoutEditor : MonoBehaviour
    {
        const int RowsPerPage = 7;
        static readonly Color Navy=new Color(.025f,.12f,.32f), Royal=new Color(.025f,.31f,.76f), Ivory=new Color(1f,.97f,.87f), Gold=new Color(1f,.8f,.16f), Pale=new Color(.78f,.89f,1f), Red=new Color(.98f,.22f,.25f);
        static readonly string[] StatNames={"PV","Attaque","Défense","Attaque spéciale","Défense spéciale","Vitesse"};
        readonly List<Selectable> controls=new List<Selectable>();
        readonly List<MoveDefinition> legalMoves=new List<MoveDefinition>();
        readonly List<ItemDefinition> heldItems=new List<ItemDefinition>();
        readonly List<Button> moveSlots=new List<Button>();
        readonly List<Text> moveLabels=new List<Text>();
        readonly List<MainMenuPanel> moveSurfaces=new List<MainMenuPanel>();
        readonly List<Text> statResults=new List<Text>();
        readonly List<MainMenuPanel> evGauges=new List<MainMenuPanel>();
        readonly List<InputField> ivInputs=new List<InputField>(), evInputs=new List<InputField>();
        readonly List<Button> visibleChoices=new List<Button>();
        RectTransform root, body, cursor; CanvasGroup fade;
        Font font; Catalog catalog; SpeciesDefinition species; TeamMember draft;
        Func<TeamMember,bool> save; Action cancel,remove; Button saveButton,closeButton;
        Text message, budget, itemLabel, summary, tabHint;
        int tab, activeMove, page; bool controller, hasMember, rebuilding; float opened;
        string query=""; GameObject previousSelection;
        public bool IsOpen => root!=null && root.gameObject.activeSelf;
        public TeamMember Draft => draft==null?null:draft.Copy();
        public Button SaveButton => saveButton;
        public IReadOnlyList<Button> MoveSlots => moveSlots;
        public int ActiveMoveSlot => activeMove;
        public int CurrentTab => tab;
        public bool CursorVisible => controller && cursor!=null && cursor.gameObject.activeSelf;
        public IReadOnlyList<Button> VisibleChoices => visibleChoices;
        public IReadOnlyList<InputField> IndividualValueInputs => ivInputs;
        public IReadOnlyList<InputField> EffortValueInputs => evInputs;
        public void SetTab(int index) { if(IsOpen && index>=0 && index<3)ShowTab(index); }
        public int RemainingEvBudget => draft==null?510:510-Total(draft.evs);

        public void Open(RectTransform parent,Font uiFont,Catalog data,TeamMember initial,Texture portrait,bool member,
            Func<TeamMember,bool> onSave,Action onCancel,Action onRemove,bool useController)
        {
            Close(false); catalog=data; font=uiFont; draft=initial.Copy();
            draft.ivs=draft.ivs??new StatValues(31); draft.evs=draft.evs??new StatValues();
            species=catalog.GetSpecies(draft.speciesId); draft.moveIds=draft.moveIds??(int[])species.moves.Clone();
            save=onSave; cancel=onCancel; remove=onRemove; hasMember=member; controller=useController;
            activeMove=page=tab=0; query=""; opened=Time.unscaledTime;
            previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            root=Rect(parent,"Atelier du partenaire",0,0,1600,900);
            var veil=Panel(root,"Rideau",0,0,1600,900,new Color(.008f,.035f,.12f,.91f)); veil.raycastTarget=true;
            var desk=Panel(root,"Atelier",136,52,1328,794,Navy); desk.BorderWidth=3; desk.BorderColor=Gold; desk.Corner=26;
            Panel(root,"Bannière atelier",150,65,1300,109,Royal);
            Ball(root,166,77,70,.52f);
            Label(root,"ATELIER DU DRESSEUR",249,75,780,23,14,Gold).fontStyle=FontStyle.Bold;
            Label(root,species.name.ToUpperInvariant()+"  <size=21>#"+species.id.ToString("000")+"</size>",247,103,1070,46,32,Ivory).fontStyle=FontStyle.Bold;
            closeButton=Button(root,"FERMER",1301,87,126,48,Cancel,15);
            Panel(root,"Carte partenaire",158,190,314,558,new Color(.035f,.20f,.45f));
            if(portrait!=null) { var image=Rect(root,"Portrait du partenaire",221,191,188,168).gameObject.AddComponent<RawImage>();image.texture=portrait;image.color=Color.white;image.raycastTarget=false; }
            else Ball(root,271,235,86,.6f);
            Label(root,"TES QUATRE CAPACITÉS",174,362,280,27,14,Gold).fontStyle=FontStyle.Bold;
            moveSlots.Clear(); moveLabels.Clear(); moveSurfaces.Clear();
            for(int i=0;i<4;i++)
            {
                int slot=i; var button=Button(root,"",172,400+i*57,286,49,()=>SelectSlot(slot),14);
                moveSlots.Add(button); moveSurfaces.Add(button.GetComponent<MainMenuPanel>());
                Label(button.transform,(i+1).ToString(),9,5,26,36,19,Gold,TextAnchor.MiddleCenter).fontStyle=FontStyle.Bold;
                moveLabels.Add(Label(button.transform,"",43,2,235,45,15,Ivory));
            }
            Label(root,"OBJET TENU",174,638,278,21,12,Pale);
            itemLabel=Label(root,"",174,663,278,30,16,Ivory); itemLabel.fontStyle=FontStyle.Bold;
            summary=Label(root,"",174,702,278,32,13,Gold);
            Button(root,"CAPACITÉS",492,190,290,48,()=>ShowTab(0),18);
            Button(root,"IV / EV",797,190,290,48,()=>ShowTab(1),18);
            Button(root,"OBJET",1102,190,326,48,()=>ShowTab(2),18);
            body=Rect(root,"Contenu de l’atelier",492,249,936,496);
            message=Label(root,"",168,753,1256,31,15,Gold);
            if(hasMember) Button(root,"RETIRER",169,793,186,37,Remove,14);
            Button(root,"ANNULER",hasMember?371:169,793,186,37,Cancel,14);
            saveButton=Button(root,hasMember?"ENREGISTRER LE PARTENAIRE":"ENREGISTRER LA FICHE",897,787,530,44,()=>Save(),17);
            cursor=Rect(root,"Curseur atelier",0,0,28,28);var icon=cursor.gameObject.AddComponent<MainMenuIcon>();icon.Kind=MainMenuIcon.Symbol.PokeBall;icon.raycastTarget=false;
            fade=root.gameObject.AddComponent<CanvasGroup>(); fade.alpha=0;
            ShowTab(0); RefreshSummary();
        }

        public void SetInputPresentation(bool useController) { controller=useController; }
        public bool SetMove(int slot,int moveId)
        {
            if(draft==null || slot<0 || slot>3) return false;
            if(moveId!=0 && Array.IndexOf(catalog.GetLearnableMoves(species.id),moveId)<0) { Notice("Cette capacité n’est pas apprise par "+species.name+"."); return false; }
            var values=new List<int>(draft.moveIds??Array.Empty<int>());
            if(moveId!=0 && values.Contains(moveId) && (slot>=values.Count || values[slot]!=moveId)) { Notice("Cette capacité occupe déjà un emplacement."); return false; }
            if(moveId==0) { if(slot<values.Count) values.RemoveAt(slot); }
            else if(slot<values.Count) values[slot]=moveId;
            else { if(slot>values.Count) { Notice("Remplis d’abord l’emplacement précédent."); return false; } values.Add(moveId); }
            draft.moveIds=values.ToArray(); RefreshSummary();
            if(moveId>0) Notice(catalog.GetMove(moveId).name+" occupe l’emplacement "+(slot+1)+".");
            return true;
        }
        public bool SetIndividualValue(int stat,int value)
        {
            if(draft==null || stat<0 || stat>5 || value<0 || value>31) return false;
            Put(draft.ivs,stat,value);RefreshStats();return true;
        }
        public bool SetEffortValue(int stat,int value)
        {
            if(draft==null || stat<0 || stat>5 || value<0 || value>252) return false;
            if(Total(draft.evs)-Get(draft.evs,stat)+value>510) { Notice("Le total d’EV est limité à 510. Libère des points dans une autre statistique."); return false; }
            Put(draft.evs,stat,value);RefreshStats();return true;
        }
        public bool SetHeldItem(string id)
        {
            if(draft==null) return false;
            ItemDefinition item;try { item=catalog.GetItem(id); }catch(ArgumentException){Notice("Cet objet ne figure pas dans le sac.");return false;}
            if(item!=null && !item.holdable && !string.Equals(item.id,"none",StringComparison.OrdinalIgnoreCase)) { Notice("Cet objet ne peut pas être tenu en combat.");return false; }
            draft.itemId=item==null || item.id=="none"?null:item.id;RefreshSummary();
            Notice(item==null?"Aucun objet tenu.":item.name+" est équipé.");return true;
        }
        public bool Save()
        {
            if(draft==null) return false;
            try { catalog.ValidateTeamMember(draft); }
            catch(ArgumentException) { Notice("Choisis une à quatre capacités, un objet tenu et des valeurs IV / EV valides.");return false; }
            if(save!=null && !save(draft.Copy())) { Notice("La fiche n’a pas pu être enregistrée.");return false; }
            Close(true);return true;
        }
        public void Cancel() { if(!IsOpen)return;Close(true);cancel?.Invoke(); }
        public void Close(bool restoreSelection)
        {
            if(root!=null) { root.gameObject.SetActive(false);Release(root.gameObject);root=null; }
            if(restoreSelection && previousSelection!=null && previousSelection.activeInHierarchy && EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(previousSelection);
            controls.Clear();moveSlots.Clear();moveLabels.Clear();moveSurfaces.Clear();ivInputs.Clear();evInputs.Clear();statResults.Clear();evGauges.Clear();visibleChoices.Clear();
            draft=null;body=null;cursor=null;
        }
        void Remove() { Close(false);remove?.Invoke(); }
        void SelectSlot(int value) { activeMove=value;ShowTab(0);RefreshSummary(); }
        void ShowTab(int value)
        {
            tab=value;page=0;query="";DrawBody();
        }
        void DrawBody()
        {
            rebuilding=true;
            for(int i=body.childCount-1;i>=0;i--) {var child=body.GetChild(i).gameObject;child.SetActive(false);Release(child);}
            ivInputs.Clear();evInputs.Clear();statResults.Clear();evGauges.Clear();budget=null;
            if(tab==0) DrawMoves();else if(tab==1) DrawStats();else DrawItems();
            rebuilding=false;LinkNavigation();
            var all=body.GetComponentsInChildren<Selectable>();if(all.Length>0)Focus(all[0]);
            RefreshSummary();
        }
        void DrawMoves()
        {
            Label(body,"APPRENTISSAGES RÉCENTS  ·  TOUTES LES MÉTHODES",0,0,660,25,14,Pale).fontStyle=FontStyle.Bold;
            tabHint=Label(body,"",0,28,936,29,14,Gold);
            var search=Search(body,"Rechercher une capacité…",0,65,620,37,value=>{query=value;page=0;DrawMoveRows();});search.SetTextWithoutNotify(query);
            Button(body,"VIDER LE SLOT",642,65,293,37,()=>{SetMove(activeMove,0);DrawMoveRows();},13);
            Rect(body,"Lignes",0,114,936,331);
            DrawMoveRows();
        }
        void DrawMoveRows()
        {
            var rows=(RectTransform)body.Find("Lignes");string selectedName=SelectedRow(rows);Clear(rows);legalMoves.Clear();visibleChoices.Clear();
            string key=Normalize(query);
            foreach(int id in catalog.GetLearnableMoves(species.id)) {var move=catalog.GetMove(id);if(key.Length==0 || Normalize(move.name).Contains(key) || Normalize(TypeName(move.type)).Contains(key))legalMoves.Add(move);}
            legalMoves.Sort((a,b)=>string.Compare(a.name,b.name,StringComparison.CurrentCultureIgnoreCase));
            int pages=Mathf.Max(1,(legalMoves.Count+RowsPerPage-1)/RowsPerPage);page=Mathf.Clamp(page,0,pages-1);
            int from=page*RowsPerPage,to=Mathf.Min(legalMoves.Count,from+RowsPerPage);
            for(int i=from;i<to;i++)
            {
                var move=legalMoves[i];bool equipped=Array.IndexOf(draft.moveIds,move.id)>=0; int id=move.id;
                var b=Button(rows,"",0,(i-from)*47,936,42,()=>{SetMove(activeMove,id);DrawMoveRows();},13);
                b.name="Capacité "+id;visibleChoices.Add(b);
                var panel=b.GetComponent<MainMenuPanel>();panel.color=equipped?new Color(.035f,.29f,.40f):Royal;panel.BorderColor=equipped?Gold:Pale;panel.Corner=12;
                var ribbon=Panel(b.transform,"Type",8,7,113,28,TypeColor(move.type));ribbon.Corner=10;
                Label(b.transform,TypeName(move.type),8,7,113,28,12,Color.white,TextAnchor.MiddleCenter).fontStyle=FontStyle.Bold;
                Label(b.transform,move.name,137,0,283,42,16,Ivory).fontStyle=FontStyle.Bold;
                Label(b.transform,CategoryName(move.category),427,0,131,42,13,Pale);
                Label(b.transform,"PUI "+(move.power>0?move.power.ToString():"—"),566,0,102,42,13,Ivory);
                Label(b.transform,"PRÉ "+(move.accuracy>0?move.accuracy+" %":"—"),669,0,115,42,13,Ivory);
                Label(b.transform,"PP "+move.pp,788,0,63,42,13,Ivory);
                Label(b.transform,(equipped?"✓\n":"")+SupportName(move.Support),854,0,74,42,10,move.Support=="implemented"?(equipped?Gold:Pale):new Color(1f,.69f,.38f),TextAnchor.MiddleCenter);
            }
            if(legalMoves.Count==0)Label(rows,"Aucune capacité pour cette recherche.",0,90,936,60,20,Pale,TextAnchor.MiddleCenter);
            Paging(body,legalMoves.Count+" capacités apprenables",pages,()=>DrawMoveRows());
            tabHint.text="EMPLACEMENT "+(activeMove+1)+"  ·  Choisis une capacité. Une même capacité ne peut pas être équipée deux fois.";
            LinkNavigation();
            RestoreRowFocus(selectedName);
        }
        void DrawItems()
        {
            Label(body,"LE SAC DU DRESSEUR",0,0,720,25,17,Ivory).fontStyle=FontStyle.Bold;
            Label(body,"Seuls les objets pouvant être tenus en combat sont proposés.",0,30,936,27,14,Pale);
            var search=Search(body,"Rechercher un objet…",0,65,620,37,value=>{query=value;page=0;DrawItemRows();});search.SetTextWithoutNotify(query);
            Button(body,"AUCUN OBJET",642,65,293,37,()=>{SetHeldItem(null);DrawItemRows();},13);
            Rect(body,"Lignes",0,114,936,331);DrawItemRows();
        }
        void DrawItemRows()
        {
            var rows=(RectTransform)body.Find("Lignes");string selectedName=SelectedRow(rows);Clear(rows);heldItems.Clear();visibleChoices.Clear();string key=Normalize(query);
            foreach(var item in catalog.items??Array.Empty<ItemDefinition>())if(item.holdable && item.id!="none" && (key.Length==0 || Normalize(item.name).Contains(key)))heldItems.Add(item);
            heldItems.Sort((a,b)=>string.Compare(a.name,b.name,StringComparison.CurrentCultureIgnoreCase));
            int pages=Mathf.Max(1,(heldItems.Count+RowsPerPage-1)/RowsPerPage);page=Mathf.Clamp(page,0,pages-1);
            for(int i=page*RowsPerPage;i<Mathf.Min(heldItems.Count,(page+1)*RowsPerPage);i++)
            {
                var item=heldItems[i];string id=item.id;bool equipped=string.Equals(id,draft.itemId,StringComparison.OrdinalIgnoreCase);
                var b=Button(rows,"",0,(i%RowsPerPage)*47,936,42,()=>{SetHeldItem(id);DrawItemRows();},14);
                b.name="Objet "+id;visibleChoices.Add(b);
                b.GetComponent<MainMenuPanel>().BorderColor=equipped?Gold:Pale;
                Label(b.transform,item.name,18,0,570,42,17,Ivory).fontStyle=FontStyle.Bold;
                Label(b.transform,ItemSupportName(item.support,equipped),591,0,327,42,12,item.support=="implemented"?(equipped?Gold:Pale):new Color(1f,.69f,.38f),TextAnchor.MiddleRight);
            }
            if(heldItems.Count==0)Label(rows,"Aucun objet pour cette recherche.",0,90,936,60,20,Pale,TextAnchor.MiddleCenter);
            Paging(body,heldItems.Count+" objets tenus",pages,()=>DrawItemRows());LinkNavigation();RestoreRowFocus(selectedName);
        }
        void Paging(RectTransform parent,string caption,int pages,Action redraw)
        {
            var old=parent.Find("Pagination");if(old!=null){old.gameObject.SetActive(false);Release(old.gameObject);}
            var footer=Rect(parent,"Pagination",0,451,936,42);
            Label(footer,caption,0,0,640,42,14,Pale);
            var previous=Button(footer,"‹",649,0,57,38,()=>{page--;redraw();FocusFirstChoice();},25);previous.interactable=page>0;
            Label(footer,(page+1)+" / "+pages,712,0,149,38,14,Gold,TextAnchor.MiddleCenter);
            var next=Button(footer,"›",868,0,57,38,()=>{page++;redraw();FocusFirstChoice();},25);next.interactable=page+1<pages;
        }
        void DrawStats()
        {
            Label(body,"ENTRAÎNEMENT  ·  NIVEAU 50",0,0,650,26,17,Ivory).fontStyle=FontStyle.Bold;
            budget=Label(body,"",0,32,936,28,15,Gold);
            Label(body,"STATISTIQUE",7,78,241,28,13,Pale);
            Label(body,"IV  ·  0 À 31",261,78,211,28,13,Pale,TextAnchor.MiddleCenter);
            Label(body,"EV  ·  0 À 252",505,78,221,28,13,Pale,TextAnchor.MiddleCenter);
            Label(body,"AU NIVEAU 50",755,78,176,28,13,Gold,TextAnchor.MiddleCenter);
            for(int i=0;i<6;i++)
            {
                int stat=i;float y=114+i*47;
                var row=Panel(body,"Ligne "+StatNames[i],0,y,936,41,new Color(.035f,.21f,.47f));row.Corner=9;
                Label(body,StatNames[i],12,y,238,41,15,Ivory).fontStyle=FontStyle.Bold;
                Button(body,"−",258,y+3,40,35,()=>SetIndividualValue(stat,Mathf.Max(0,Get(draft.ivs,stat)-1)),19);
                var iv=ValueInput(body,309,y+3,103,35,Get(draft.ivs,i),value=>SetIndividualValue(stat,value));ivInputs.Add(iv);
                Button(body,"+",422,y+3,40,35,()=>SetIndividualValue(stat,Mathf.Min(31,Get(draft.ivs,stat)+1)),19);
                Button(body,"−",499,y+3,40,35,()=>SetEffortValue(stat,Mathf.Max(0,Get(draft.evs,stat)-4)),19);
                var ev=ValueInput(body,550,y+3,104,35,Get(draft.evs,i),value=>SetEffortValue(stat,value));evInputs.Add(ev);
                Button(body,"+",666,y+3,40,35,()=>SetEffortValue(stat,Mathf.Min(252,Get(draft.evs,stat)+4)),19);
                statResults.Add(Label(body,"",755,y,164,41,23,Gold,TextAnchor.MiddleCenter));
                Panel(body,"EV repère",508,y+37,194,3,new Color(.12f,.32f,.57f));
                var gauge=Panel(body,"EV entraînement",508,y+37,0,3,Gold);gauge.Corner=1;evGauges.Add(gauge);
            }
            Button(body,"IV : TOUS À 31",0,416,283,39,()=>{draft.ivs=new StatValues(31);RefreshStats();},14);
            Button(body,"IV : TOUS À 0",300,416,283,39,()=>{draft.ivs=new StatValues();RefreshStats();},14);
            Button(body,"EFFACER LES EV",600,416,334,39,()=>{draft.evs=new StatValues();RefreshStats();},14);
            Label(body,"± EV : pas de 4. Saisie exacte possible. Nature neutre pour ce prototype.",0,462,936,28,13,Pale);
            RefreshStats();
        }
        void RefreshStats()
        {
            if(draft==null)return;
            for(int i=0;i<ivInputs.Count;i++){ivInputs[i].SetTextWithoutNotify(Get(draft.ivs,i).ToString());evInputs[i].SetTextWithoutNotify(Get(draft.evs,i).ToString());evGauges[i].rectTransform.sizeDelta=new Vector2(194*Get(draft.evs,i)/252f,3);}
            if(budget!=null)budget.text="EV INVESTIS : "+Total(draft.evs)+" / 510     ·     "+RemainingEvBudget+" POINTS DISPONIBLES";
            if(statResults.Count==6)
            {
                var b=species.stats;int[] bases={b.hp,b.attack,b.defense,b.specialAttack,b.specialDefense,b.speed};
                for(int i=0;i<6;i++) {int value=(2*bases[i]+Get(draft.ivs,i)+Get(draft.evs,i)/4)*50/100+(i==0?60:5);if(i==0 && bases[i]==1)value=1;statResults[i].text=value.ToString();}
            }
            RefreshSummary();
        }
        void RefreshSummary()
        {
            if(draft==null)return;
            for(int i=0;i<moveLabels.Count;i++)
            {
                bool filled=i<draft.moveIds.Length;var move=filled?catalog.GetMove(draft.moveIds[i]):null;
                moveLabels[i].text=filled?move.name+"\n<size=11><color=#C7E3FF>"+TypeName(move.type)+" · "+CategoryName(move.category)+"</color>"+(move.Support=="implemented"?"":" <color=#FFB061>· "+SupportName(move.Support)+"</color>")+"</size>":"EMPLACEMENT LIBRE";
                moveSurfaces[i].BorderColor=tab==0 && i==activeMove?Gold:new Color(.50f,.69f,.93f);
            }
            var item=catalog.GetItem(draft.itemId);if(itemLabel!=null)itemLabel.text=item==null || item.id=="none"?"Aucun objet":item.name+(item.support=="implemented" || string.IsNullOrEmpty(item.support)?"":"\n<size=10><color=#FFB061>"+ItemSupportName(item.support,false)+"</color></size>");
            if(summary!=null)summary.text=draft.moveIds.Length+" / 4 capacités  ·  EV "+Total(draft.evs)+" / 510";
            if(saveButton!=null)saveButton.interactable=draft.moveIds.Length>0;
        }
        void Notice(string text) {if(message!=null)message.text=text;}
        static string SelectedRow(Transform rows){var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;return selected!=null && selected.transform.IsChildOf(rows)?selected.name:null;}
        void RestoreRowFocus(string name){if(name==null)return;foreach(var choice in visibleChoices)if(choice.name==name){Focus(choice);return;}FocusFirstChoice();}
        void FocusFirstChoice(){if(visibleChoices.Count>0)Focus(visibleChoices[0]);}
        void LinkNavigation()
        {
            if(root==null)return;controls.Clear();controls.AddRange(root.GetComponentsInChildren<Selectable>());controls.RemoveAll(c=>!c.IsActive()||!c.IsInteractable());
            foreach(var control in controls)control.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=Neighbor(control,Vector2.left),selectOnRight=Neighbor(control,Vector2.right),selectOnUp=Neighbor(control,Vector2.up),selectOnDown=Neighbor(control,Vector2.down)};
        }
        Selectable Neighbor(Selectable source,Vector2 direction)
        {
            var r=(RectTransform)source.transform;Vector2 origin=root.InverseTransformPoint(r.TransformPoint(r.rect.center));float best=float.PositiveInfinity;Selectable result=null;
            foreach(var candidate in controls){if(candidate==source)continue;var other=(RectTransform)candidate.transform;Vector2 delta=(Vector2)root.InverseTransformPoint(other.TransformPoint(other.rect.center))-origin;float forward=Vector2.Dot(delta,direction);if(forward<1)continue;float lateral=Mathf.Abs(delta.x*direction.y-delta.y*direction.x);float score=forward+lateral*3;if(score<best){best=score;result=candidate;}}return result;
        }
        void Update()
        {
            if(!IsOpen)return;float now=Time.unscaledTime;fade.alpha=1-Mathf.Pow(1-Mathf.Clamp01((now-opened)/.18f),3);
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            bool show=controller && selected!=null && selected.transform.IsChildOf(root);cursor.gameObject.SetActive(show);
            if(show){var r=selected.transform as RectTransform;if(r!=null){var p=root.InverseTransformPoint(r.TransformPoint(new Vector3(r.rect.xMin,r.rect.center.y)));cursor.localPosition=p+new Vector3(-20+Mathf.Sin(now*5)*2,0,0);}}
            // The modal remains the sole navigation surface until Save or Cancel.
            if(selected!=null && !selected.transform.IsChildOf(root))Focus(closeButton);
        }
        void OnDestroy(){Close(false);}
        InputField Search(Transform parent,string placeholder,float x,float y,float w,float h,Action<string> changed)
        {
            var r=Rect(parent,"Recherche",x,y,w,h);var p=Panel(r,"Champ",0,0,w,h,Ivory);p.BorderWidth=2;p.BorderColor=Royal;p.raycastTarget=true;
            var input=r.gameObject.AddComponent<InputField>();input.targetGraphic=p;input.textComponent=Label(r,"",12,0,w-24,h,15,Navy);input.placeholder=Label(r,placeholder,12,0,w-24,h,15,new Color(.29f,.43f,.59f));input.characterLimit=45;input.onValueChanged.AddListener(v=>{if(!rebuilding)changed(v);});return input;
        }
        InputField ValueInput(Transform parent,float x,float y,float w,float h,int initial,Func<int,bool> apply)
        {
            var input=Search(parent,"0",x,y,w,h,_=>{});input.contentType=InputField.ContentType.IntegerNumber;input.characterLimit=3;input.textComponent.alignment=TextAnchor.MiddleCenter;input.SetTextWithoutNotify(initial.ToString());
            input.onEndEdit.AddListener(value=>{int number;if(!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out number)||!apply(number)){Notice("Valeur refusée. IV : 0 à 31 ; EV : 0 à 252, total 510 maximum.");RefreshStats();}});return input;
        }
        Button Button(Transform parent,string text,float x,float y,float w,float h,Action action,int size)
        {
            var r=Rect(parent,text.Length==0?"Choix":text,x,y,w,h);var p=r.gameObject.AddComponent<MainMenuPanel>();p.color=Royal;p.Corner=14;p.BorderWidth=2;p.BorderColor=Ivory;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=p;var colors=b.colors;colors.selectedColor=colors.highlightedColor=new Color(1,.9f,.56f);colors.disabledColor=new Color(.44f,.55f,.70f,.82f);b.colors=colors;b.onClick.AddListener(()=>action());
            Label(r,text,7,1,w-14,h-2,size,Ivory,TextAnchor.MiddleCenter).fontStyle=FontStyle.Bold;return b;
        }
        MainMenuPanel Panel(Transform parent,string name,float x,float y,float w,float h,Color tint){var panel=Rect(parent,name,x,y,w,h).gameObject.AddComponent<MainMenuPanel>();panel.color=tint;panel.raycastTarget=false;return panel;}
        Text Label(Transform parent,string text,float x,float y,float w,float h,int size,Color color,TextAnchor align=TextAnchor.MiddleLeft){var t=Rect(parent,"Texte",x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.alignment=align;t.supportRichText=true;t.raycastTarget=false;return t;}
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h){var r=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
        static void Ball(Transform parent,float x,float y,float size,float alpha){var r=Rect(parent,"Poké Ball",x,y,size,size);var icon=r.gameObject.AddComponent<MainMenuIcon>();icon.Kind=MainMenuIcon.Symbol.PokeBall;icon.raycastTarget=false;r.gameObject.AddComponent<CanvasGroup>().alpha=alpha;}
        static void Focus(Selectable target){if(target!=null && target.IsActive() && target.IsInteractable() && EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(target.gameObject);}
        static void Clear(Transform parent){for(int i=parent.childCount-1;i>=0;i--){var child=parent.GetChild(i).gameObject;child.SetActive(false);Release(child);}}
        static void Release(GameObject item){if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        static int Total(StatValues values)=>values.hp+values.attack+values.defense+values.specialAttack+values.specialDefense+values.speed;
        static int Get(StatValues values,int index){switch(index){case 0:return values.hp;case 1:return values.attack;case 2:return values.defense;case 3:return values.specialAttack;case 4:return values.specialDefense;default:return values.speed;}}
        static void Put(StatValues values,int index,int value){switch(index){case 0:values.hp=value;break;case 1:values.attack=value;break;case 2:values.defense=value;break;case 3:values.specialAttack=value;break;case 4:values.specialDefense=value;break;default:values.speed=value;break;}}
        static string Normalize(string text){var b=new StringBuilder();foreach(char c in (text??"").Normalize(NormalizationForm.FormD))if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)b.Append(char.ToLowerInvariant(c));return b.ToString();}
        static string SupportName(string support)=>support=="implemented"?"OK":support=="partial"?"PARTIEL":"À COMPLÉTER";
        static string ItemSupportName(string support,bool equipped)=> (equipped?"ÉQUIPÉ · ":"")+(support=="partial"?"EFFET PARTIEL":support=="unsupported"?"EFFET À COMPLÉTER":"OBJET TENU");
        static string CategoryName(string c)=>string.Equals(c,"Physical",StringComparison.OrdinalIgnoreCase)?"Physique":string.Equals(c,"Special",StringComparison.OrdinalIgnoreCase)?"Spéciale":"Statut";
        static string TypeName(string t){switch(t){case "Grass":return "Plante";case "Fire":return "Feu";case "Water":return "Eau";case "Electric":return "Électrik";case "Bug":return "Insecte";case "Poison":return "Poison";case "Ground":return "Sol";case "Flying":return "Vol";case "Psychic":return "Psy";case "Fighting":return "Combat";case "Rock":return "Roche";case "Ghost":return "Spectre";case "Ice":return "Glace";case "Dragon":return "Dragon";case "Fairy":return "Fée";case "Steel":return "Acier";case "Dark":return "Ténèbres";default:return "Normal";}}
        static Color TypeColor(string type){switch(type){case "Grass":return new Color(.20f,.64f,.28f);case "Fire":return new Color(.95f,.32f,.13f);case "Water":return new Color(.13f,.52f,.93f);case "Electric":return new Color(.76f,.51f,.01f);case "Poison":return new Color(.63f,.24f,.70f);case "Flying":return new Color(.47f,.45f,.83f);case "Bug":return new Color(.48f,.62f,.12f);case "Ground":return new Color(.66f,.43f,.22f);case "Psychic":return new Color(.90f,.26f,.48f);case "Fighting":return new Color(.72f,.24f,.19f);case "Rock":return new Color(.56f,.49f,.30f);case "Ghost":return new Color(.36f,.29f,.66f);case "Ice":return new Color(.13f,.65f,.72f);case "Dragon":return new Color(.31f,.27f,.79f);case "Fairy":return new Color(.77f,.33f,.62f);case "Steel":return new Color(.36f,.51f,.59f);case "Dark":return new Color(.31f,.26f,.32f);default:return new Color(.48f,.48f,.43f);}}
    }
}
