# MD TAOS ADMIN — Reskin Dashboard responsive

**Date :** 2026-05-17
**Statut :** Validé (direction visuelle + design approuvés)
**Base :** Évolution de [2026-05-16-md-taos-admin-design.md](2026-05-16-md-taos-admin-design.md) (prototype existant, livré sur `main`).
**Nature :** **Reskin** — change la présentation, pas le comportement.

---

## 1. Contexte & objectif

Le prototype actuel a un rendu « app mobile » (coquille téléphone `max-width:480px`, barre
d'onglets en bas). L'utilisateur veut un **site dashboard responsive** : look site admin
pro, agréable sur **desktop, tablette et mobile**, pas une web-app de téléphone.

Objectif : refaire **uniquement la présentation** (layouts, design system, navigation,
mise en forme du contenu) sans toucher aux fonctionnalités, parcours, données ou tests
de comportement.

## 2. Portée & garanties (NON négociable)

**Inchangé :** les 23 routes/écrans, les parcours utilisateur, tout le code `@code`
des pages, `IDataService` + `InMemoryDataService` + `SeedData`, `AuthState`,
`NavigationGuard`, les modèles, et la logique métier. Les **29 tests doivent rester
verts** à la fin.

**Modifié :** `wwwroot/app.css` (réécrit), les layouts, les composants de navigation,
et le **balisage de présentation** des pages (structure visuelle : grilles, stat-cards,
tableaux responsives) — sans modifier la logique ni les méthodes.

**Supprimé :** la contrainte coquille `.app-shell{max-width:480px}`, le composant
`BottomNav`, l'ancien `TopBar` (remplacés — voir §4).

**Hors périmètre :** Firebase, nouvelles fonctionnalités, refonte de l'IA, nouveaux écrans.

## 3. Fonctionnalité prioritaire — pointage de shift employé

C'est la fonctionnalité **cœur** : elle doit rester **présente, prioritaire et très
visible** côté employé après le reskin, avec une formulation UI claire et cohérente.

Parcours (le jour de l'event, employé **assigné/confirmé**) :

1. **Accueil employé** + **détail event** mettent en avant un bouton primaire
   **« Commencer mon shift »**.
2. Shift en cours : écran dédié avec **chrono qui tourne** + bouton
   **« Terminer mon shift »**.
3. **Récap** affiché à la fin, formulé exactement ainsi (données dynamiques) :
   > **Event {nom} · {date} · Rôle {rôle}**
   > Tu as travaillé de **{hh:mm}** à **{hh:mm}**, soit **{X h YY}** au total.
   puis bouton primaire **« Envoyer ma timesheet pour validation »**.
4. Après envoi : confirmation claire (« Timesheet envoyée pour validation »).

Comportement déjà implémenté (`EHome`, `EEventDetail`, `EActive`, `ERecap`,
`InMemoryDataService`, `Timesheet.Duration`) : on **conserve la logique**, on **améliore
le libellé** (« Commencer mon shift » / « Terminer mon shift » / phrase de récap
ci-dessus / « Envoyer ma timesheet pour validation ») et la **mise en avant**
(bouton primaire pleine largeur, écran de shift épuré centré sur le chrono).

Côté manager : la timesheet envoyée apparaît dans « Timesheets à valider » (table)
→ détail → valider / corriger les heures / refuser (inchangé fonctionnellement).

## 4. Layout & navigation

**Shell unique `DashboardLayout`** (menu selon le rôle via `AuthState.CurrentUser`).
Remplace `ManagerLayout`/`EmployeeLayout`/`BottomNav`/`TopBar`.

- **Sidebar** sombre dorée, fixe à gauche, **desktop ≥ 1024 px** : wordmark TAOS or,
  items de menu (icône + libellé), item actif en or, pied « Nom · Rôle ».
  - Manager : Tableau de bord · Events · Équipe · Timesheets · Rôles · Profil
  - Employé : Tableau de bord · Events · Mes heures · Profil
- **Tablette 768–1023 px** : sidebar réduite en **rail d'icônes** (libellés masqués,
  tooltip/au survol facultatif).
- **Mobile < 768 px** : sidebar masquée ; **topbar** avec bouton **☰** ouvrant un
  **drawer** (overlay sombre, sidebar pleine glissant depuis la gauche, fermeture
  par clic overlay ou navigation).
- **Topbar** claire (toutes tailles) : titre de la page courante à gauche,
  utilisateur + avatar (initiales) à droite ; sur mobile le ☰ précède le titre.
- **Pages auth** (`/`, `/login`, `/register`, `/pending`) : layout clair **sans
  sidebar** — panneau carte centré (largeur max ~420 px) sur fond clair, wordmark TAOS.

Composants : `Sidebar.razor`, `Topbar.razor` (NOUVEAU composant de layout, distinct
de l'ancien `TopBar.razor` supprimé), `DashboardLayout.razor`, `AuthShell.razor`
(layout auth). État du drawer mobile géré localement (paramètre de layout + petit
service d'UI ou cascade) — pas de logique métier.

**Migration de l'ancien `TopBar`** : presque toutes les pages rendent aujourd'hui
`<TopBar ShowBack/Title/BackTo>` dans leur markup. Ce composant est supprimé ; chaque
page retire son `<TopBar …>`. Le titre de page est désormais fourni par la `Topbar`
du layout. La **navigation retour** des écrans détail/formulaire est préservée via un
lien/bouton « ‹ Retour » dans le panneau (utilisant la même cible que l'ancien
`BackTo`) ou la sidebar — aucune perte de navigation. C'est une modification de
markup de présentation (autorisée §2), la logique des pages ne change pas.

## 5. Design system (clair & pro, accents or) — `app.css` réécrit

**Palette**
- Fond page `#F4F5F7` · surface/carte `#FFFFFF` · bordure `#E6E8EC`
- Texte `#1F2329` · atténué `#6B7280`
- Or principal `#B8902F` · or clair `#C2A14D` · fond or doux `rgba(194,161,77,.13)`
- Sidebar fond `#17181B` · texte sidebar `#C9CDD4` · actif or `#E7C76B`
- Statuts : ambre `#FFF4DE`/`#B8902F`, succès `#E6F4EA`/`#2E7D32`, refus `#FBEAEA`/`#C0392B`
- CTA primaire : dégradé `linear-gradient(135deg,#C2A14D,#B8902F)`, texte blanc

**Typo** : Playfair Display 700 (wordmark, titres de page, chrono, gros chiffres) ;
Inter (UI, corps, tableaux). Polices **déjà locales** dans `wwwroot/fonts` (conservées).

**Composants visuels** : sidebar, topbar, carte/panel, **stat-card** (grand chiffre +
libellé), **tableau** (en-tête gris clair, lignes séparées, hover), **pill** de statut,
bouton primaire/secondaire/danger, champ de formulaire clair, **stepper**, **toggle**,
**chrono** (grand, doré), état vide, label de section, drawer + overlay.

**Layout & responsive** : conteneur de contenu fluide avec padding (pas de
`max-width:480px`) ; grilles `stat-cards` `repeat(auto-fit,minmax(...))` ;
breakpoints **1024** (sidebar pleine) / **768** (rail → drawer ; tables → cartes) ;
zones tactiles ≥ 40 px ; respect `env(safe-area-inset-*)`.

**PWA** : manifest/service-worker conservés ; `theme_color` mis à jour vers le clair
(`#F4F5F7`) et meta `theme-color` cohérente ; écran de chargement passe au clair
(fond `#F4F5F7`, wordmark or). Reste installable, polices/icônes locales (zéro réseau
runtime — exigence conservée).

## 6. Contenu responsive — composants

**`ResponsiveTable<T>`** (réutilisable, approche encapsulée) : reçoit une définition de
colonnes (libellé + fragment de cellule) et une liste d'éléments + un fragment d'action
optionnel + un fragment « état vide ». Rendu : **tableau** (desktop/tablette),
**cartes empilées** (mobile, < 768 px) via CSS (libellés de colonne injectés en
`::before`/`data-label`, pas de JS). Lignes/cartes cliquables (navigation conservée).
Utilisé par : Manager Events, Équipe, Timesheets ; Employé Mes heures, Events.

**`StatCard`** : grand chiffre or + libellé, cliquable (NavLink) — utilisé sur les
accueils Manager (à traiter) et Employé.

Pages **détail / formulaire / récap / shift en cours / auth** : panneaux carte
centrés (largeur max lisible, ~520–640 px), pas de tableau.

Chaque page liste passe d'un `@foreach` de cartes à `<ResponsiveTable …>` en
**réutilisant les mêmes données et la même navigation** déjà présentes dans `@code`.

## 7. Impact sur les tests

Tests de logique (Model/DataService/AuthState/NavigationGuard) : **aucun impact**
(aucune logique modifiée).

Tests bUnit composant (3) : ils ciblent du markup.
- `Login_lists_five_seeded_test_accounts` attend 5 éléments `.card.quick` →
  le markup du Login reskinné **conservera un sélecteur équivalent** (classe
  `.quick-login` ou conservation de `.card.quick`) **ou** le test sera mis à jour
  pour cibler le nouveau sélecteur (même intention : 5 comptes, libellés
  Manager/En attente/Refusé présents).
- `Stepper_increments_and_clamps_at_min`, `Chrono_formats_elapsed_since_start` :
  composants conservés ; classes `.stepper`/`.chrono` conservées → tests inchangés.

**Exigence : `dotnet test` = 29/29 verts à la fin** (mise à jour minimale et
documentée d'un test seulement si un point d'ancrage de markup change réellement).

## 8. Écrans (mapping reskin)

Les 23 routes inchangées. Présentation cible par groupe :

- **Auth** (4) : `AuthShell` clair centré.
- **Manager Accueil** : grille de `StatCard` (comptes à valider, demandes,
  timesheets à valider, events à venir) + table « Timesheets à valider » +
  liste prochains events.
- **Manager Events / Équipe / Timesheets** : `ResponsiveTable` (+ filtres/onglets
  conservés) ; bouton primaire « + Nouvel event » etc.
- **Manager Détail event / employé / timesheet / Rôles / Édition / Assigner /
  Profil** : panneaux carte centrés ; actions = boutons clairs.
- **Employé Accueil** : `StatCard`/encart « prochaine prestation » avec le bouton
  prioritaire **Commencer mon shift** (cf. §3) ; `ResponsiveTable` events à venir.
- **Employé Events / Détail event** : table + détail panneau ; CTA pointage (§3).
- **Employé Shift en cours** : écran épuré centré, gros chrono, **Terminer mon shift**.
- **Employé Récap** : panneau centré avec la phrase de récap exacte (§3) +
  **Envoyer ma timesheet pour validation**.
- **Employé Mes heures / Détail timesheet / Profil** : `ResponsiveTable` + panneau.

## 9. Critères de validation (definition of done)

- `dotnet build` (solution) **0 erreur** ; `dotnet test` **29/29 verts**.
- Aucune route/parcours/logique modifié ; diff limité à CSS, layouts, composants
  de nav, markup de présentation, libellés du parcours shift (§3).
- Rendu **dashboard** : sidebar sombre dorée + contenu clair ; responsive vérifié à
  **≥1024 px**, **~800 px (tablette)**, **375 px (mobile)** — sidebar→rail→drawer,
  tables→cartes, aucun débordement, zones tactiles confortables.
- Fonctionnalité §3 **présente et mise en avant** avec les libellés exacts.
- PWA toujours installable, écran de chargement clair, zéro dépendance réseau runtime.
- `IDataService` reste la seule porte de données ; pages toujours fines.

## 10. Notes pour la planification

Découpage suggéré : (1) design system `app.css` clair + tokens ; (2) `Sidebar`/
`Topbar`/`DashboardLayout`/`AuthShell` + suppression `BottomNav`/ancien `TopBar` +
bascule des `@layout` ; (3) `ResponsiveTable` + `StatCard` ; (4) bascule des pages
listes vers `ResponsiveTable` (Manager events/équipe/timesheets, Employé heures/events) ;
(5) accueils en `StatCard` + tables ; (6) parcours shift §3 (libellés + mise en avant)
+ pages détail/form/récap/auth en panneaux ; (7) PWA clair + passe responsive finale
(1024/800/375) + maintien 29 tests verts.
