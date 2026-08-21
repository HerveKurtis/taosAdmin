# Responsable du jour & profil enrichi — design

Date : 2026-08-21
Statut : validé pour implémentation, sauf un point ouvert (§B.6)

## Contexte

AdminTaos est aujourd'hui une application à deux faces : `AccountType` décide
à la fois des droits et de l'espace habité (`/m/*` pour les Admins, `/e/*` pour
les Collaborateurs), et `NavigationGuard.Resolve` expulse quiconque sort de son
espace. Toute la conception ci-dessous préserve ce modèle binaire.

Deux besoins sont traités ensemble parce que le second dépend du premier : un
responsable du jour doit être joignable, donc son téléphone doit exister avant
que la fonctionnalité ait un sens.

## Décisions prises

| Question | Décision |
|---|---|
| Nature du rôle de responsable | Désignation **par event**, pas un type de compte |
| Qui est sélectionnable | Tous les comptes **actifs**, Admins compris, avec recherche |
| Ce que le responsable peut faire | Voir son équipe, pointer les présences, lire les consignes |
| Validation des heures par le responsable | **Hors périmètre** — l'Admin reste seul à valider |
| Pointage et chronomètre | **Indépendants** : présence ≠ heures |

Le rôle d'Admin reste de la supervision : il ne prend pas de shift et n'apparaît
pas dans les listes d'assignation. Être responsable du jour n'est pas un shift,
c'est pourquoi un Admin peut l'être.

---

## Chantier A — Profil enrichi

### A.1 Modèle

Trois champs optionnels sur `Account` :

```csharp
public string? Phone { get; set; }
public string? PostalAddress { get; set; }
public string? Iban { get; set; }
```

Optionnels au sens strict : un compte sans aucun des trois reste parfaitement
valide. Rien dans l'application ne doit exiger leur présence.

### A.2 Écrans

Un panneau « Mes informations » sous la photo, identique dans `EProfile.razor`
et `MProfile.razor` : trois champs et un bouton Enregistrer. Un Admin y a droit
aussi — s'il peut être désigné responsable du jour, son téléphone doit être
renseigné.

Les trois valeurs s'affichent en lecture seule dans `MEmployeeDetail.razor`,
qui est l'écran depuis lequel une paie se prépare.

### A.3 Validation

Le téléphone et l'adresse sont du texte libre, simplement `Trim()`.

L'IBAN est normalisé avant enregistrement — majuscules, espaces supprimés — et
refusé s'il ne correspond pas à `^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$`. Un IBAN
erroné se paie en virement raté ; le coût d'un refus à la saisie est bien
inférieur. La validation reste structurelle : pas de contrôle de la clé
modulo 97, qui apporterait peu au regard de sa complexité.

Un champ vidé est enregistré comme `null`, pas comme chaîne vide.

### A.4 Sécurité

**Aucune modification des règles Firestore.** Elles autorisent déjà un compte à
se modifier lui-même hors `type` et `status`, et tout Admin à lire l'intégralité
des comptes.

Conséquence assumée : **tout Admin peut lire l'IBAN de n'importe qui.** C'est
cohérent avec le rôle d'employeur. Restreindre cette lecture à certains Admins
supposerait une notion de permission fine qui n'existe pas dans le modèle et
n'est pas demandée.

### A.5 Tests

bUnit sur `EProfile` et `MProfile` :

- la saisie des trois champs persiste dans `IDataService`
- les champs laissés vides restent permis et sont stockés à `null`
- un IBAN malformé est refusé, le compte n'est pas modifié, un message s'affiche
- un IBAN valide saisi avec espaces et minuscules est normalisé
- `MEmployeeDetail` affiche les trois valeurs

---

## Chantier B — Responsable du jour

### B.1 Modèle

Sur `ServiceEvent` :

```csharp
public string? ResponsableAccountId { get; set; }
```

`OnSiteContact` **reste dans le modèle** mais disparaît du formulaire d'édition.
Les events existants portent du texte libre utile (`"Julie — 0470 00 00 00"`)
qu'une suppression du champ détruirait. `MEventDetail` affiche le responsable
désigné quand il existe, et retombe sur `OnSiteContact` sinon. Même traitement
que `RoleNeed.HourlyRate` : la donnée survit, l'édition disparaît.

Sur `Assignment` :

```csharp
public PresenceStatus Presence { get; set; } = PresenceStatus.Expected;
```

Avec `public enum PresenceStatus { Expected, Present, Absent }`. Les documents
Firestore existants n'ont pas ce champ ; `System.Text.Json` conserve alors la
valeur de l'initialiseur, donc tout assignment ancien se lit comme `Expected`.
Aucune migration de données n'est nécessaire.

### B.2 Désignation

Dans `MEventEdit.razor`, un panneau « Responsable du jour » remplace le champ
« Contact sur place » : une zone de recherche, et en dessous la liste filtrée
des comptes actifs (Admins et Collaborateurs) affichée en `button.card`, du même
type que le sélecteur de rôles métier déjà utilisé dans `MEmployeeDetail`.

La recherche filtre sur le nom et l'email, sans distinction de casse. La
sélection est unique et réversible : un bouton « Aucun responsable » remet le
champ à `null`. Tant qu'aucune recherche n'est saisie, la liste est tronquée aux
huit premiers comptes par ordre alphabétique pour ne pas noyer l'écran.

### B.3 Page responsable

Un seul composant, `EventTeam.razor`, exposé sous deux routes :

```razor
@page "/e/events/{Id}/equipe"
@page "/m/events/{Id}/equipe"
```

C'est le point de conception qui évite de toucher à `NavigationGuard` : celui-ci
n'inspecte que le premier segment du chemin, donc un Collaborateur atteint la
route `/e/…` et un Admin la route `/m/…`, sans qu'aucune règle de navigation ne
change. Un Admin désigné responsable a donc accès à la page, alors qu'il est
expulsé de tout le reste de `/e/*`.

Accès autorisé si l'utilisateur courant est le responsable désigné de l'event,
ou s'il est Admin — la supervision reste possible. Sinon, la page affiche un
refus explicite, sans redirection silencieuse.

Contenu :

- Rappel de l'event : lieu, adresse, horaires, consignes, effectif attendu
- L'équipe assignée et confirmée, groupée par rôle métier
- Par personne : photo ou initiales, nom, rôle, téléphone en lien `tel:`
- Par personne : deux boutons, Présent et Absent, qui basculent `Presence`
- En tête de liste, le décompte : « 3 présents · 1 absent · 2 attendus »

Un collaborateur sans téléphone renseigné affiche « Téléphone non renseigné »,
pas un lien mort.

### B.4 Points d'entrée

Sur `/e/events/{id}`, un collaborateur qui est le responsable désigné voit un
bandeau « Tu es responsable du jour » et un bouton « Gérer mon équipe ».

Sur `/m/events/{id}`, l'Admin voit le nom du responsable, le décompte des
présences, et un bouton vers la même page.

### B.5 Vue admin

`MEventDetail` affiche le responsable désigné à la place de « Contact » quand il
existe, et ajoute une ligne de décompte des présences sous l'effectif par rôle.

### B.6 Règles Firestore — un point ouvert et une extension

**Écriture — décidé.** Le responsable doit pouvoir écrire dans l'`Assignment`
d'un autre collaborateur, mais uniquement le champ `presence` :

```
function eventOf(eid) {
  return get(/databases/$(db)/documents/events/$(eid)).data;
}
function isResponsableOf(eid) {
  return isEmployee() && eventOf(eid).responsableAccountId == request.auth.uid;
}

allow update: if isManager()
              || (isResponsableOf(resource.data.eventId)
                  && request.resource.data.diff(resource.data)
                         .affectedKeys().hasOnly(['presence']));
```

Subtilité à ne pas manquer à l'implémentation : `FirestoreDataService` écrit via
`setDoc`, pas `updateDoc`. Ce n'est pas un problème — `diff().affectedKeys()`
compare les valeurs et ne retient que celles qui changent réellement, donc un
`setDoc` réécrivant les autres champs à l'identique satisfait `hasOnly`.

**Lecture — à trancher.** Aujourd'hui un Collaborateur ne lit que ses propres
assignments. Le responsable doit lire ceux de toute son équipe. Deux voies :

1. *Recommandé* — aligner `assignments` sur ce que fait déjà `events` :
   lecture ouverte à tout compte actif, l'interface filtrant ce qu'elle montre.
   Le commentaire « v1 simplification » existe déjà sur `events` pour cette
   raison exacte. Simple, sans coût.
2. Rule conditionnelle appelant `isResponsableOf` à la lecture. Plus étroit,
   mais chaque document évalué déclenche un `get()`, et Firestore plafonne à
   20 `get()` par requête. Un event à plus d'une dizaine d'assignments ferait
   échouer la requête entière. Écarté pour cette raison.

La voie 1 signifie qu'un collaborateur curieux pourrait lire la liste complète
des assignations de tous les events. **Ce point demande une validation
explicite avant implémentation.**

### B.7 Tests

bUnit :

- `MEventEdit` : la recherche filtre sur nom et email ; sélectionner un compte
  enregistre `ResponsableAccountId` ; « Aucun responsable » le remet à `null`
- `EventTeam` : le responsable désigné accède à la page ; un collaborateur
  quelconque est refusé ; un Admin accède
- `EventTeam` : bascule Présent puis Absent, la valeur persiste, le décompte suit
- `EventTeam` : un collaborateur sans téléphone n'affiche pas de lien `tel:`
- `EEventDetail` : le bandeau et le bouton n'apparaissent que pour le responsable
- `MEventDetail` : affiche le responsable, retombe sur `OnSiteContact` sinon

Les règles Firestore ne sont pas couvertes par la suite bUnit, qui s'exécute
contre `InMemoryDataService`. Elles seront vérifiées manuellement contre
l'émulateur Firestore avant déploiement. C'est le point faible de ce plan de
test et il est assumé : monter une suite d'émulateur dépasse le périmètre.

---

## Hors périmètre

Explicitement exclus, à ne pas glisser dans l'implémentation :

- Validation des heures par le responsable — l'Admin reste seul juge
- Démarrage d'un shift à la place d'un collaborateur
- Notifications, SMS, emails
- Calcul de rémunération, malgré la présence de `RoleNeed.HourlyRate` et de
  l'IBAN — les deux sont stockés, aucun n'est exploité
- Historique ou journal des pointages : `Presence` porte l'état courant, pas ses
  transitions

## Ordre d'implémentation

1. Chantier A en entier — le téléphone conditionne l'utilité de B
2. Chantier B, modèle et désignation (B.1, B.2)
3. Chantier B, page responsable et points d'entrée (B.3, B.4, B.5)
4. Règles Firestore (B.6), vérifiées à l'émulateur, puis déploiement

Chaque étape est livrable indépendamment : à l'issue de l'étape 2 la
désignation fonctionne et s'affiche, même si la page d'équipe n'existe pas
encore.
