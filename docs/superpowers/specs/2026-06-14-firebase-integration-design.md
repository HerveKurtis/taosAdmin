# MD TAOS ADMIN — Intégration Firebase v1

**Date :** 2026-06-14
**Statut :** Validé (portée et décisions structurantes approuvées)
**Base :** Évolution de [2026-05-16-md-taos-admin-design.md](2026-05-16-md-taos-admin-design.md) (prototype) et [2026-05-17-md-taos-admin-dashboard-reskin-design.md](2026-05-17-md-taos-admin-dashboard-reskin-design.md) (reskin dashboard, livré sur branche `dashboard-reskin`).
**Nature :** Remplacement de la couche données et de l'auth mock par un vrai backend Firebase. Le seam `IDataService` était déjà conçu pour ça.

---

## 1. Contexte & objectif

L'application tourne aujourd'hui sur :
- Auth mock (`AuthState` + `Blazored.LocalStorage`) : email-only, mot de passe ignoré, session locale.
- Données en mémoire (`InMemoryDataService : IDataService` + `SeedData`).

Objectif : passer à une vraie persistence cross-device et à une vraie authentification, sans toucher aux écrans, parcours, markup, ni à la logique métier des pages. Le seam `IDataService` rend l'opération mécanique côté UI.

## 2. Portée & garanties

**Inclus en v1 :**
- **Firebase Authentication** (Email + mot de passe).
- **Cloud Firestore** comme implémentation de `IDataService` (toutes les collections : `accounts`, `jobRoles`, `events`, `assignments`, `timesheets`). Plus aucune donnée en mémoire en prod.
- **Firebase Storage** pour les photos de profil utilisateur.
- **Règles de sécurité Firestore strictes** basées sur rôle + statut.
- **Règles de sécurité Storage** : un utilisateur authentifié écrit uniquement son propre avatar ; lecture publique-authentifiée.

**Exclus en v1 (peuvent venir plus tard) :** listeners Firestore temps-réel, persistence offline Firestore, FCM push notifs, Firebase Hosting, Cloud Functions, Firebase App Check, custom claims, MFA.

**Garanties non négociables :**
- Aucun `@code` de page n'est modifié.
- Aucune route ni parcours UI ne change.
- Le reskin dashboard reste visuellement identique.
- `dotnet build` 0 erreur. `dotnet test` **28/28 verts** (cf. §6).
- Les valeurs publiques de config Firebase (apiKey, etc.) **ne sont pas des secrets** — la sécurité vient des règles. Documenté pour éviter toute confusion.

## 3. Bootstrap & cycle de vie des comptes

L'app est livrée **vide** : aucun document seedé en production.

**Premier démarrage :**
1. Tu déploies l'app pointant sur un projet Firebase neuf. Auth Email/Password activé. Firestore créé en mode production. Aucune collection préexistante.
2. Tu (Hervé) t'inscris via `/register` : Firebase Auth crée ton compte, l'app crée `accounts/{uid}` avec `type="Employee", status="Pending"`. Tu atterris sur `/pending`.
3. Tu ouvres la console Firebase → Firestore → `accounts/{ton-uid}` et tu mets manuellement `type: "Manager"` et `status: "Active"`. C'est la **seule** action « hors app » du bootstrap.
4. Tu te re-connectes. L'`AuthState` lit le doc à jour → tu accèdes à `/m`.
5. Tu crées les `jobRoles` (Serveur, Hôtesse, …) via l'écran existant `/m/roles`.
6. Tu valides les inscriptions Pending au fur et à mesure dans `/m/team`. Tu peux nommer d'autres managers en éditant leur doc directement en console (étape 3 répétée si besoin).

**Important :** si tu changes le `type`/`status` d'un utilisateur déjà connecté pendant sa session, sa page actuelle ne le verra pas en temps réel (pas de listener en v1). Il doit recharger la page ou se reconnecter. C'est volontaire (YAGNI) ; documenté.

## 4. Architecture

**Choix de SDK :** Firebase JS SDK v9+ modulaire, chargé directement dans `index.html`, appelé depuis C# via `IJSRuntime`. **Pas** de package Blazor tiers (zéro dépendance, contrôle total, suit les évolutions Firebase).

**Nouveaux fichiers C# :**
- `src/AdminTaos/Services/IAuthClient.cs` — interface fine (`LoginAsync`, `RegisterAsync`, `LogoutAsync`, `GetCurrentUidAsync`, événement `OnAuthChanged`).
- `src/AdminTaos/Services/FirebaseAuthClient.cs` — implémentation prod via `IJSRuntime`.
- `src/AdminTaos/Services/FirestoreDataService.cs` — implémentation prod de `IDataService` via `IJSRuntime`.
- `src/AdminTaos/wwwroot/firebase.js` — module ES qui importe le SDK Firebase v9 (CDN), expose les fonctions appelables depuis Blazor (`firebaseLogin`, `firebaseRegister`, `firestoreGetAccounts`, etc.) via `window.taos = { ... }` ou export. Initialise l'app Firebase depuis `firebase-config.js`.
- `src/AdminTaos/wwwroot/firebase-config.js` — config Firebase publique (apiKey, authDomain, projectId, storageBucket, messagingSenderId, appId). Commit (pas un secret).

**Fichiers C# modifiés :**
- `src/AdminTaos/Services/AuthState.cs` — refactor : constructeur prend `IAuthClient` + `IDataService` (au lieu de l'ancien email-only). Nouvelle signature `LoginAsync(email, password)` et `RegisterAsync(name, email, password, roleIds)`. `InitializeAsync()` écoute `OnAuthChanged` du client, recharge `accounts/{uid}` à chaque changement d'auth pour avoir Type/Status à jour.
- `src/AdminTaos/Pages/Auth/Login.razor` — `DoLogin()` appelle `Auth.LoginAsync(_email, _password)`. **La connexion rapide est supprimée** (les 5 boutons `<button class="card quick">` disparaissent, ainsi que la section « Comptes de test »). Page : champ email + champ mot de passe + bouton « Se connecter » + lien « Créer un compte » (vers `/register`).
- `src/AdminTaos/Pages/Auth/Register.razor` — `Submit()` appelle `Auth.RegisterAsync(name, email, pwd, roleIds)`. Sur succès : redirige vers `/pending`.
- `src/AdminTaos/Program.cs` — registre : `AddSingleton<IAuthClient, FirebaseAuthClient>()`, `AddSingleton<IDataService, FirestoreDataService>()`, `AddScoped<AuthState>()`, `AddScoped<NavigationGuard>()`. `Blazored.LocalStorage` n'est plus nécessaire (session gérée par Firebase Auth) — on l'enlève proprement.
- `src/AdminTaos/wwwroot/index.html` — ajout d'un `<script type="module" src="firebase.js"></script>` avant le chargement Blazor.

**Fichiers C# inchangés :** `JobRole`, `RoleNeed`, `ServiceEvent`, `Assignment`, `Timesheet`, tous les enums, `IDataService`, `NavigationGuard`, `ViewHelpers`, tous les composants (`ResponsiveTable`, `StatCard`, `Stepper`, `ToggleSwitch`, `Chrono`, `EmptyState`, `SectionLabel`), tous les layouts (`DashboardLayout`, `AuthShell`), `App.razor`, et **les `@code` de toutes les 23 pages d'événements/timesheets/équipe**.

**Petites évolutions modèle / composants pour la photo de profil :**
- `Account` gagne un champ `string? PhotoUrl` (optionnel, null par défaut).
- `Sidebar.razor` foot : affiche l'avatar (img si PhotoUrl, sinon initiales) à côté du nom.
- `DashTopbar.razor` : affiche l'img si `accounts/{uid}.photoUrl` est non null, sinon les initiales (comportement actuel).
- `MProfile.razor` + `EProfile.razor` : une section avatar avec bouton « Changer ma photo » qui ouvre le sélecteur de fichier → upload via `IStorageClient` → mise à jour de `accounts/{uid}.photoUrl`.
- Nouveau service `IStorageClient` + impl. `FirebaseStorageClient` (upload via Firebase Storage JS SDK + IJSRuntime).

**Conservé pour tests :** `InMemoryDataService.cs` et `SeedData.cs` restent dans le repo. Ils ne sont plus enregistrés en DI prod mais le projet de test les instancie directement (déjà le cas aujourd'hui).

## 5. Schéma Firestore

Collections au top level (pas de sous-collections en v1) :

- **`accounts/{uid}`** — clé doc = Firebase Auth uid.
  - `fullName: string`, `email: string`, `type: "Manager"|"Employee"`, `status: "Pending"|"Active"|"Rejected"`, `jobRoleIds: string[]`, `photoUrl: string?` (URL signée Firebase Storage, ou `null` → fallback initiales), `createdAt: Timestamp`.
- **`jobRoles/{id}`** — id généré client (ex. Guid).
  - `name: string`, `color: string` (hex).
- **`events/{id}`** — id généré client.
  - `name`, `venue`, `address`, `date: string "yyyy-MM-dd"` (DateOnly), `meetingTime: string "HH:mm"` (TimeOnly), `expectedEndTime: string "HH:mm"`, `dressCode`, `instructions`, `onSiteContact`, `roleNeeds: [{ jobRoleId, countNeeded, hourlyRate }]` (array d'objets), `isOpenForSignup: bool`, `status: "Upcoming"|"InProgress"|"Past"`, `createdAt: Timestamp`.
- **`assignments/{id}`** — id généré client.
  - `eventId`, `accountId`, `jobRoleId`, `source: "AssignedByManager"|"SelfRequest"`, `status: "PendingApproval"|"Confirmed"|"Rejected"`.
- **`timesheets/{id}`** — id généré client.
  - `assignmentId`, `startedAt: Timestamp?`, `endedAt: Timestamp?`, `status: "NotStarted"|"InProgress"|"ToSend"|"Sent"|"Validated"|"Rejected"`, `managerAdjustedStart: Timestamp?`, `managerAdjustedEnd: Timestamp?`, `rejectionReason: string?`, `sentAt: Timestamp?`, `validatedAt: Timestamp?`.

**Conventions :**
- Enums sérialisés en `string` PascalCase (lisibles en console et dans les règles).
- `DateOnly`/`TimeOnly` en `string` ISO (préserves la sémantique « pas de timezone »).
- `DateTime` en `Timestamp` Firestore.
- Pas d'index composé requis en v1 (les requêtes restent simples : par `eventId`, par `accountId`, par `status`).

## 5b. Photos de profil

**Storage layout :** `avatars/{uid}.jpg` (un seul fichier par utilisateur, écrasé à chaque upload — pas d'historique en v1).

**Flux d'upload (Profil) :**
1. L'utilisateur clique « Changer ma photo » → input file (JPEG/PNG).
2. Côté client : redimensionnement Canvas à max 512×512, ré-encodage JPEG qualité ~0.85 (la photo finale fait typiquement 50–300 Ko).
3. Upload sur `avatars/{uid}.jpg` via Firebase Storage JS SDK.
4. Récupère l'URL de téléchargement (`getDownloadURL`).
5. Met à jour `accounts/{uid}.photoUrl` avec cette URL.
6. `AuthState.Refresh()` (ou re-lecture du doc) → la sidebar/topbar/profil se mettent à jour.

**Affichage :**
- **`Sidebar.razor` foot** : un `<img class="ava">` (rond, ~28 px, src=PhotoUrl) si PhotoUrl, sinon `<span class="ava-i">{Initiales}</span>`. À côté : nom · rôle.
- **`DashTopbar.razor` avatar** : `<img>` si PhotoUrl, sinon le `<span class="av">@Initials</span>` existant.
- **Page Profil** : avatar grand format (96 px), bouton « Changer ma photo », message d'erreur si upload échoue.

**Initiales (fallback)** : algorithme `string.Concat((FullName ?? "").Split(' ', RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])))` — déjà implémenté dans `DashTopbar`. Pour « Hervé Tendayi » → « HT » ; pour « Marc » → « M » ; vide → « · ». Réutilisé tel quel.

**Limites :**
- Taille max upload : 2 Mo brut (rejeté côté client si plus gros) ; après compression la cible est <500 Ko.
- Pas de crop interactif en v1 (le carré central de l'image source est utilisé). Bouton crop éventuel en v2.
- Pas de suppression explicite en v1 : remplacer par une nouvelle photo écrase l'ancienne ; revenir aux initiales = supprimer manuellement le fichier en console (ou via une suppression côté app, à ajouter si demandé).

## 6. Règles de sécurité (baseline strict)

`firestore.rules` (esquisse — la version finale du plan d'implémentation aura le code complet) :

```
rules_version = '2';
service cloud.firestore {
  match /databases/{db}/documents {
    function userDoc()    { return get(/databases/$(db)/documents/accounts/$(request.auth.uid)).data; }
    function isAuth()     { return request.auth != null; }
    function isActive()   { return isAuth() && userDoc().status == "Active"; }
    function isManager()  { return isActive() && userDoc().type == "Manager"; }
    function isEmployee() { return isActive() && userDoc().type == "Employee"; }
    function isSelf(uid)  { return isAuth() && request.auth.uid == uid; }

    // Accounts
    match /accounts/{uid} {
      allow read:   if isSelf(uid) || isManager();
      allow create: if isSelf(uid)
                    && request.resource.data.type == "Employee"
                    && request.resource.data.status == "Pending";
      allow update: if isManager()
                    || (isSelf(uid)
                        && request.resource.data.type   == resource.data.type
                        && request.resource.data.status == resource.data.status);
      allow delete: if isManager();
    }

    // Job roles
    match /jobRoles/{id} {
      allow read:  if isActive();
      allow write: if isManager();
    }

    // Events
    match /events/{id} {
      allow read:  if isManager()
                   || (isEmployee()
                       && (resource.data.isOpenForSignup == true
                           || hasAssignmentOnEvent(id)));
      allow write: if isManager();
    }

    // Assignments
    match /assignments/{id} {
      allow read:   if isManager()
                    || (isEmployee() && resource.data.accountId == request.auth.uid);
      allow create: if isManager()
                    || (isEmployee()
                        && request.resource.data.accountId == request.auth.uid
                        && request.resource.data.source    == "SelfRequest"
                        && request.resource.data.status    == "PendingApproval");
      allow update: if isManager(); // employé ne modifie pas son assignment lui-même
      allow delete: if isManager();
    }

    // Timesheets
    match /timesheets/{id} {
      allow read:   if isManager() || ownsTimesheet(id);
      allow create: if isEmployee() && createdForOwnAssignment();
      allow update: if isManager()
                    || (isEmployee() && ownsTimesheet(id) && employeeCanEdit());
      allow delete: if isManager();
    }
  }
}
```

(Les helpers `hasAssignmentOnEvent`, `ownsTimesheet`, `createdForOwnAssignment`, `employeeCanEdit` sont à implémenter via `get()`/`exists()` dans les rules — détaillés dans le plan d'implémentation. Le principe est : un employé Active ne peut lire/éditer **que** ses propres assignments/timesheets, ne peut créer une demande de participation que pour lui-même avec source=SelfRequest+status=PendingApproval, ne peut éditer sa timesheet que dans les transitions autorisées — Start → InProgress, Finish → ToSend, Send → Sent, Resend si Rejected.)

Les règles seront commitées dans `firestore.rules` à la racine du repo et déployées via `firebase deploy --only firestore:rules`.

**Règles Storage** (`storage.rules`) :

```
rules_version = '2';
service firebase.storage {
  match /b/{bucket}/o {
    match /avatars/{uid}.jpg {
      allow read:  if request.auth != null;
      allow write: if request.auth != null
                   && request.auth.uid == uid
                   && request.resource.size < 2 * 1024 * 1024
                   && request.resource.contentType.matches('image/.*');
    }
  }
}
```

Déployées via `firebase deploy --only storage`.

## 7. Tests

**Inchangés (restent verts) :**
- `ModelTests` (4 tests).
- `DataServiceTests` (6 tests) — testent `InMemoryDataService` directement, qui reste dans le repo pour ce besoin.
- `NavigationGuardTests` (9 tests).
- `Chrono_formats_elapsed_since_start` (1 test bUnit).
- `Stepper_increments_and_clamps_at_min` (1 test bUnit).

**Refactorés (restent verts après modification) :**
- `AuthStateTests` (4 tests). `AuthState` devient testable via `IAuthClient`. Un `FakeAuthClient` (en mémoire) remplace l'ancien `FakeLocalStorage` du contexte AuthState. Les 4 tests couvrent : login avec email connu, login email inconnu, init restaure la session, logout efface. La sémantique change légèrement (login prend (email,pwd)) mais la couverture reste.

**Supprimé :**
- `Login_lists_five_seeded_test_accounts` (1 test bUnit) — il vérifiait l'existence des 5 boutons `.card.quick`, fonctionnalité retirée. Pas de perte de couverture (le test pointait une fonctionnalité de prototype dégagée).

**Bilan : 29 → 28 tests verts.**

**Pas de tests d'intégration Firebase en v1.** Les tests qui voudraient parler à Firestore réel/émulateur sont hors scope (ajoutent du Node + émulateur). Les règles seront validées par tests manuels (cf. §9). Une suite d'émulateur peut s'ajouter en v2.

## 8. Config & dev

**Setup Firebase :**
1. Créer un projet Firebase (plan **Spark** gratuit).
2. Activer **Authentication → Email/Password**.
3. Créer une base **Cloud Firestore** en mode production.
4. Activer **Storage** (mode production).
5. Récupérer la config web depuis « Paramètres du projet → Vos applications → SDK setup » et la coller dans `src/AdminTaos/wwwroot/firebase-config.js` :
   ```js
   export const firebaseConfig = {
     apiKey: "AIzaSy...",
     authDomain: "<project>.firebaseapp.com",
     projectId: "<project-id>",
     storageBucket: "<project>.appspot.com",
     messagingSenderId: "...",
     appId: "1:..."
   };
   ```
5. Installer le CLI : `npm install -g firebase-tools`. `firebase login`. `firebase init firestore` (pour `firestore.rules` + `firestore.indexes.json`).
6. Déployer les règles : `firebase deploy --only firestore:rules,storage`.

**Dev local :** le même projet Firebase qu'en démo. Pas d'émulateur en v1 (ajoutable plus tard). Les tests automatisés restent isolés grâce à `InMemoryDataService`.

**Coûts :** Spark plan gratuit : 50 K reads/jour, 20 K writes/jour, 1 Go stockage. Largement assez pour un usage prototype/démo.

**Hébergement :** hors scope. Tu peux déployer le build WASM Release n'importe où (Firebase Hosting, GitHub Pages, Netlify, S3+CloudFront, etc.). À décider plus tard.

## 9. Critères de validation (DoD)

- `dotnet build` 0 erreur ; `dotnet test` **28/28 verts**.
- Build Release publie sans erreur et sert sur `localhost` ; `firebase-config.js`, `firebase.js`, `app.css`, `manifest.webmanifest` sont dans `wwwroot/`.
- **Cycle bout-en-bout réel sur Firestore :**
  1. Inscription d'un nouvel utilisateur → doc `accounts/{uid}` créé `Pending` → écran `/pending`.
  2. Edition manuelle console : type=Manager, status=Active.
  3. Re-login → accès `/m`, création de 2 jobRoles, création d'un event, validation d'une inscription Pending d'un employé.
  4. Login employé → `/e` montre l'event, « Commencer mon shift » fonctionne → /active → « Terminer mon shift » → /recap → « Envoyer ma timesheet pour validation ».
  5. Manager voit la timesheet dans `/m/timesheets`, peut Valider ou Refuser (motif).
  6. Employé voit le résultat dans `/e/hours` (Refusée → motif visible + Renvoyer).
- **Photo de profil :**
  7. Sur page Profil, upload d'une photo de profil → l'avatar sidebar + topbar passent de « initiales » à l'image. Reload → l'image persiste.
  8. Un utilisateur sans photo voit ses initiales partout (sidebar foot, topbar avatar, page Profil). Algorithme d'initiales vérifié pour les cas « Prénom Nom » → 2 lettres, « Prénom » → 1 lettre, vide → « · ».
  9. Tentative d'upload d'un fichier non-image ou > 2 Mo : rejetée côté client avant l'appel Storage (message d'erreur dans la page Profil).
- **Tests négatifs de règles** (manuel, via console rules-playground ou un compte employé) : un employé ne peut PAS lire la timesheet d'un autre employé ; un Pending ne peut PAS lister `accounts`/`events` ; un employé Active ne peut PAS écrire dans `events`.
- Reskin et UI inchangés visuellement à toutes les tailles.
- Le markup `<button class="card quick">` est absent de `Login.razor` (la connexion rapide est retirée).

## 10. Notes pour la planification

Découpage suggéré pour le plan d'implémentation :
1. **Setup Firebase + config** : créer projet, activer Auth/Firestore/Storage, ajouter `firebase-config.js`, `firebase.js` (init + helpers Auth/Firestore/Storage), `firestore.rules` + `storage.rules` minimaux (allow auth-only pour faire passer le setup), wiring `index.html`. Pas encore de C# — juste l'infra JS et la config. Commit.
2. **`IAuthClient` + `FirebaseAuthClient` + refactor `AuthState`** : nouvelle interface, impl. JS interop, `AuthState` accepte `(email, password)`, événement `OnAuthChanged` recharge `accounts/{uid}`. Refactor `AuthStateTests` avec `FakeAuthClient`. Build + 28 tests verts.
3. **`Login.razor` + `Register.razor`** : suppression quick-login (et du test bUnit associé), wiring login Firebase, wiring register Firebase + création du doc `accounts/{uid}`. Build + 28 tests verts (le bUnit supprimé n'apparaît plus).
4. **`FirestoreDataService`** : implémente les ~23 méthodes de `IDataService` une par une (Accounts, JobRoles, Events, Assignments, Timesheets) via JS interop avec mapping Timestamp↔DateTime, string↔DateOnly/TimeOnly, enums string↔C#. Build + 28 tests verts (toujours via `InMemoryDataService` côté tests).
5. **DI swap dans `Program.cs`** : enregistrer `FirestoreDataService` à la place de `InMemoryDataService` en prod. Retirer la dépendance `Blazored.LocalStorage` du `csproj` (Firebase Auth persiste la session via IndexedDB de son côté, plus rien ne l'utilise). Build + tests verts.
6. **Photo de profil — `Account.PhotoUrl` + `IStorageClient` + `FirebaseStorageClient` + UI** : ajout du champ modèle, du service d'upload, redimensionnement Canvas, intégration sidebar foot + topbar avatar + page Profil avec bouton « Changer ma photo ». Build + 28 tests verts.
7. **Règles complètes** : `firestore.rules` strictes finales (§6) + `storage.rules` (§6 fin), commit, `firebase deploy --only firestore:rules,storage`.
8. **Vérification bout-en-bout** (DoD §9) : scénario complet sur Firebase réel incl. upload photo + initiales fallback, validation manuelle des règles, fix éventuels. Commit final.
