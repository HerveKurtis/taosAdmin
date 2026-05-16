# MD TAOS ADMIN — Spec de conception

**Date :** 2026-05-16
**Statut :** Validé (3 sections de design + choix techniques approuvés)
**Livrable :** Prototype cliquable, application **Blazor WebAssembly PWA**, mobile-first, en français.

---

## 1. Contexte & objectif

TAOS — *The Art of Service* est une société de prestation de service (événementiel /
hôtellerie). L'application gère le personnel : le manager crée des events, y affecte
du personnel (ou l'employé demande à y participer), l'employé pointe ses heures sur
place et envoie sa timesheet, le manager la valide.

**Objectif de cette itération :** un prototype cliquable et navigable, construit
directement comme le vrai projet Blazor WASM PWA, avec des **données factices en
mémoire** derrière une **couche de données interchangeable**. Aucune partie n'est
jetable : le backend Firebase se branchera plus tard sans réécrire l'UI.

## 2. Périmètre

**Inclus**
- Deux rôles applicatifs : **Manager** et **Employé**.
- Inscription employé → **validation par le manager** (compte en attente → actif).
- Gestion des rôles métier (Serveur, Hôtesse + rôles personnalisés créés par le manager).
- Création/édition d'events par le manager (infos de base, effectif par rôle,
  tenue/consignes/contact, rémunération, ouvert ou non à l'inscription).
- Affectation : le manager assigne du personnel **et** l'employé demande à participer
  → **validation par le manager**.
- Pointage employé : Commencer → Terminer → récap → Envoyer la timesheet.
- Côté manager : réception des timesheets, **valider / corriger les heures / refuser**.
- Tous les écrans, parcours et états limites listés en §6–§8.
- PWA installable, design « Noir & luxe », responsive (mobile-first).

**Hors périmètre (plus tard)**
- Backend Firebase réel et authentification/sécurité réelles (couche prévue, non câblée).
- Notifications push, export paie/facturation, localisation EN, upload photo,
  pointage par géolocalisation, gestion multi-managers/organisations.

## 3. Choix techniques & architecture

- **.NET 10** (SDK 10.0.103 + workload `wasm-tools` présents sur la machine).
- **Blazor WebAssembly** avec template **PWA** (manifest + service worker, installable,
  fonctionne hors-ligne sur l'app shell — adapté à un usage mobile sur site avec
  réseau instable).
- **Couche de données interchangeable** : interface `IDataService` (CRUD comptes,
  rôles, events, affectations, timesheets, + auth mock). Deux implémentations :
  - `InMemoryDataService` (utilisée maintenant) — données seedées en mémoire.
  - `FirebaseDataService` (plus tard) — même interface, aucun changement d'UI.
- **Auth mock** : pas de sécurité réelle. « Connexion » = email d'un compte seedé
  (mot de passe ignoré). « Inscription » crée un compte Employé en statut *En attente*.
  Session persistée en `localStorage` (l'app reste connectée après reload PWA).
- **Comptes de test en accès rapide** : l'écran de connexion affiche la liste des
  comptes seedés sous forme de boutons « connexion rapide » — chacun montrant
  **nom · type (Manager/Employé) · rôle(s) métier · statut** (Manager, Employé
  serveur actif, Employé hôtesse actif, Employé en attente, Employé refusé). Un
  clic connecte directement sans saisie, pour tester chaque profil instantanément.
- **State** : un service applicatif scoped (`AppState`/`AuthState`) expose
  l'utilisateur courant et notifie les composants.
- **Structure projet** (indicative) :
  - `Models/` — entités & enums
  - `Services/` — `IDataService`, `InMemoryDataService`, `AuthState`, seed
  - `Layout/` — shells Manager & Employé (top bar + bottom tab bar), shell auth
  - `Pages/` — pages routées (voir §6)
  - `Components/` — composants UI réutilisables (carte, CTA, stepper, toggle,
    pill de statut, chrono, tab bar, état vide)
  - `wwwroot/` — `manifest.json`, service worker, icônes (générées depuis
    `logo/taoslogo.png`), polices **embarquées localement** (pas de CDN au runtime),
    `app.css` (design system)

## 4. Rôles & permissions

- **Manager** : tout créer/voir/valider. Onglets : Accueil · Events · Équipe · Profil.
- **Employé** : voir ses events / events ouverts, demander à participer, pointer,
  envoyer ses timesheets, voir son historique. Onglets : Accueil · Events ·
  Mes heures · Profil.
- **Garde de route** : redirection selon `AccountType` et `Status`. Un employé
  *En attente* ne voit que l'écran « Compte en attente » + déconnexion.

## 5. Modèle de données

**Account** — `Id`, `FullName`, `Email`, `AccountType` (Manager | Employee),
`Status` (Pending | Active | Rejected), `JobRoleIds` (rôles métier que l'employé
peut tenir), `CreatedAt`.

**JobRole** — `Id`, `Name` (ex. « Serveur », « Hôtesse »), `Color`. Seedé avec
Serveur + Hôtesse ; le manager peut créer/éditer/supprimer.

**Event** — `Id`, `Name`, `Venue`, `Address`, `Date`, `MeetingTime` (RDV),
`ExpectedEndTime`, `DressCode`, `Instructions`, `OnSiteContact`,
`RoleNeeds` (liste de `{ JobRoleId, CountNeeded, HourlyRate }` — la
rémunération est **par rôle**), `IsOpenForSignup` (bool),
`Status` (Upcoming | InProgress | Past), `CreatedAt`.
La rémunération est visible par l'employé sur le détail de l'event.

**Assignment** — `Id`, `EventId`, `AccountId`, `JobRoleId`,
`Source` (AssignedByManager | SelfRequest),
`Status` (Confirmed | PendingApproval | Rejected).
Manager assigne → Confirmed. Employé demande → PendingApproval → manager
valide → Confirmed / Rejected.

**Timesheet** — `Id`, `AssignmentId`, `StartedAt`, `EndedAt`, `Duration` (dérivée),
`Status` (NotStarted | InProgress | ToSend | Sent | Validated | Rejected),
`ManagerAdjustedStart?`, `ManagerAdjustedEnd?`, `RejectionReason?`, `SentAt?`,
`ValidatedAt?`.
Transitions : NotStarted →(Commencer)→ InProgress →(Terminer)→ ToSend
→(Envoyer)→ Sent →(manager)→ Validated **ou** Rejected.
Le manager peut ajuster début/fin avant de valider (statut Validated, valeurs
ajustées conservées). Rejected affiche un motif et repasse l'employé en ToSend
(renvoi possible).

**Seed** : 1 manager actif, plusieurs employés (mix actifs / en attente / 1 rejeté),
Serveur + Hôtesse, 3–4 events (à venir / en cours / passé, ouverts et fermés),
affectations variées, timesheets dans tous les statuts — pour que **tous les
écrans et états soient visibles** sans manipulation.

## 6. Carte des écrans (routes)

**Commun (non connecté)**
1. Splash (logo, redirection auto)
2. Connexion — saisie email + liste des **comptes de test seedés** en accès
   rapide (nom · type · rôle(s) · statut), 1 clic = connexion
3. Inscription (nom, email, mot de passe, rôle(s) métier souhaité(s))
4. Compte en attente de validation (+ déconnexion)

**Manager** — shell : top bar + bottom tabs `Accueil · Events · Équipe · Profil`
5. Accueil — tableau « À traiter » (comptes à valider, demandes de participation,
   timesheets à valider) + prochains events
6. Events — liste
7. Détail event — infos, effectif (assignés vs requis par rôle), personnel assigné,
   demandes de participation à valider, bouton « Assigner du personnel »
8. Créer / éditer un event — formulaire complet
9. Assigner du personnel — sélection d'employés actifs par rôle → confirmer
10. Équipe — liste employés (onglets Actifs / En attente)
11. Détail employé — infos, rôles, valider / refuser le compte
12. Gérer les rôles — liste + créer / éditer / supprimer un rôle métier
13. Timesheets — liste (accès depuis l'Accueil), filtrable par statut
14. Détail timesheet — heures modifiables (steppers), durée recalculée,
    Refuser (avec motif) / Valider
15. Profil — infos manager, déconnexion

**Employé** — shell : top bar + bottom tabs `Accueil · Events · Mes heures · Profil`
16. Accueil — prochain event / event en cours mis en avant avec Commencer/Terminer ;
    raccourcis
17. Events — mes events assignés + events ouverts à rejoindre
18. Détail event — infos ; si ouvert & non assigné → « Demander à participer » ;
    si assigné → statut ; le jour J → contrôle de pointage
19. Prestation en cours — chrono, bouton Terminer
20. Récap & envoi — durée totale, début/fin, bouton Envoyer la timesheet
21. Mes heures — historique avec statuts (en cours · à envoyer · envoyée ·
    validée · refusée)
22. Détail timesheet (employé) — lecture seule ; motif si refusée ; renvoi possible
23. Profil — infos, rôle(s), statut du compte, déconnexion

## 7. Parcours cœur

1. **Onboarding** : Inscription → compte *Pending* → écran « en attente » →
   manager valide (écran 11) → compte *Active* → accès au shell employé.
2. **Event & affectation** : manager crée l'event (8) → assigne du personnel (9, →
   Assignment Confirmed) **ou** l'employé demande (18, → PendingApproval) →
   manager valide depuis le détail event (7) → Confirmed.
3. **Pointage** : le jour J l'employé ouvre l'event (18/16) → `Commencer`
   (Timesheet InProgress) → écran chrono (19) → `Terminer` (ToSend) → récap (20)
   → `Envoyer` (Sent).
4. **Validation** : manager voit la timesheet (13/14) → ajuste éventuellement
   début/fin → `Valider` (Validated) ou `Refuser` + motif (Rejected → l'employé
   voit le motif en 22 et peut renvoyer).

## 8. États limites & vides

Compte en attente / refusé ; aucun event assigné ; aucun event ouvert ;
demande de participation en attente / refusée ; effectif d'event incomplet ;
timesheet refusée (motif affiché) ; listes vides (Équipe, Timesheets, Mes heures) ;
event passé (pointage désactivé). Chaque liste a un état vide explicite et soigné.

## 9. Système de design — « Noir & luxe »

**Palette**
- Fond `#141414` · surface/carte `#1F1F1F` · carte « glow »
  dégradé `#211D12 → #1A1A1A` · bordure `#2A2A2A` · bordure or `#3A2F16`
- Texte `#EDEDED` · texte atténué `#9A9A9A`
- Or principal `#E7C76B` · or profond `#C2A14D` / `#B8902F`
- Dégradé CTA `linear-gradient(135deg,#E7C76B,#C2A14D)` sur texte sombre `#1A1500`
- Succès / live `#7BD88F`

**Typographie** — *Playfair Display* (700/600) : wordmark, titres, chrono ;
*Inter* : corps & UI. Polices **embarquées dans `wwwroot`** (offline PWA).

**Composants UI** — top bar (retour + titre, ou wordmark TAOS) ; bottom tab bar
fixe (4 onglets, actif en or) ; carte standard / carte « glow » ; label
mini-majuscule ; pill de statut ; bouton CTA plein (or) et fantôme (contour or) ;
stepper `– valeur +` ; toggle ; affichage chrono ; ligne de champ
clé→valeur ; état vide illustré.

**Layout & responsive** — mobile-first, colonne unique, largeur de contenu max
~480px centrée sur desktop, respect des safe-area insets, bottom nav fixe.

**PWA** — `manifest.json` (name « TAOS — The Art of Service », short_name
« TAOS », `display: standalone`, `theme_color`/`background_color` `#141414`,
icônes 192/512 + maskable générées depuis `logo/taoslogo.png`) ; service worker
(cache app shell) ; installable iOS/Android.

## 10. Vérification (definition of done)

- `dotnet build` vert ; l'app se lance et s'installe comme PWA.
- Parcours cliquables de bout en bout en viewport mobile (375 px) **et** desktop :
  onboarding, création/affectation event, demande de participation + validation,
  pointage complet, envoi + validation/correction/refus de timesheet.
- Toutes les routes de §6 atteignables ; gardes de route correctes
  (employé en attente bloqué, séparation Manager/Employé).
- Données seedées rendant **tous les états de §8 visibles** sans bidouille.
- `IDataService` est la seule porte d'accès aux données (aucun accès direct au
  store dans l'UI) — Firebase pourra s'y substituer sans toucher aux pages.
- Aucune dépendance réseau au runtime (polices/icônes locales).

## 11. Notes pour la planification

Périmètre cohérent pour **un seul plan d'implémentation** (un prototype d'app),
à découper en phases par le plan : (1) socle projet + design system + PWA +
`IDataService`/seed, (2) auth mock + onboarding + gardes, (3) espace Manager
(events, équipe, rôles), (4) espace Employé (events, pointage), (5) cycle
timesheet (envoi ↔ validation/correction/refus), (6) états vides + passe finale
responsive/visuelle.
