# Vérifications des manettes

Ces tests Unity EditMode synchrones isolent les périphériques Input System avec `InputTestFixture`. Ils créent des manettes virtuelles Xbox/par défaut, DualShock 4, DualSense et Switch Pro, puis envoient des événements d'état complets dans leurs layouts natifs.

Les vérifications portent sur les indications affichées, le branchement et le retrait, la dernière manette active, les événements Submit/Cancel de `InputSystemUIInputModule`, le repli clavier et le comportement pause/annulation sur une pression unique.

La présentation se trouve actuellement dans `Assembly-CSharp`. L'assembly de tests utilise la réflexion à cette frontière pour trouver `ControllerHints` et appeler sa petite interface publique. Les actions et événements UI utilisent les API Unity typées habituelles. L'appel privé à `Update` reproduit le rafraîchissement normal après un changement de manette active.

## Exécution

Lancer l'assembly `AeroStadium.ControllerChecks` en EditMode dans le Unity Test Runner. `InputTestFixture` ne prend pas en charge les tests EditMode en coroutine : les tests utilisent donc NUnit `[Test]` avec des mises à jour explicites de l'entrée.

Quand EditMode n'a pas initialisé le cache EventSystem, la fixture appelle le cycle habituel Awake/OnEnable/OnDisable du module UI. Elle active l'asset d'actions réellement affecté et traite le module par son API publique ; les vérifications Submit/Cancel passent ainsi par les vrais bindings.

La fixture possède un asset créé par `DefaultInputActions` du paquet, au lieu d'utiliser l'asset partagé par défaut du module. Son nettoyage EditMode peut donc appeler `DestroyImmediate`. Submit et Cancel utilisent des fixtures séparées : les mises à jour dynamiques manuelles avancent l'état des entrées, tandis que les tests NUnit synchrones n'avancent pas `Time.frameCount`, également vérifié par le module UI.

L'overload `InputSystem.Update(InputUpdateType.Dynamic)` du paquet est interne ; la fixture l'appelle par réflexion, sans injecter ni supprimer d'état d'action. Les tests de navigation envoient aussi le D-pad et le stick analogique dans le vrai module et vérifient les événements reçus par `IMoveHandler`.

Ces tests vérifient les correspondances logicielles et la gestion des événements. Les essais matériels USB/Bluetooth, pilotes, vibrations et illustrations des boutons restent distincts.
