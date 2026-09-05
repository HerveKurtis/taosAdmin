# À faire

## Mettre à jour la page d'aide

La FAQ de `/e/aide` et `/m/aide` a été écrite le 21 août. Plusieurs livraisons
depuis ne s'y trouvent pas, et ses captures d'écran datent d'avant les statuts
colorés — un utilisateur qui compare l'aide à son écran ne reconnaît plus ce
qu'il voit.

Les captures se refont en lançant l'application en local (`dotnet run` dans
`src/AdminTaos`), qui bascule alors sur le jeu de démonstration : Marc D.,
Sarah K., Hôtel Plaza. **Ne jamais capturer la production** — les écrans réels
portent de vrais noms, téléphones et IBAN.

### Ce qui manque, catégorie par catégorie

**Mon service**
- La pause : prendre une pause, reprendre le service, plusieurs pauses par
  prestation, le temps presté qui se calcule net.
- Le récapitulatif de fin, qui détaille désormais amplitude, pause et temps
  presté — la capture actuelle montre une seule durée.
- La fermeture automatique : un service oublié se referme huit heures après la
  fin de l'event, à l'heure de fin de l'event. Dire que ces heures apparaissent
  comme telles à l'admin, et qu'il vaut mieux terminer soi-même.

**Responsable du jour**
- Le pilotage des compteurs : démarrer le service de quelqu'un qui a oublié, à
  une heure modifiable, l'envoyer en pause, le faire reprendre, l'arrêter.
- Le refus de démarrer quelqu'un donné absent, et quoi faire dans ce cas.
- Le verrou des 48 h après la fin du service, au-delà duquel seul un admin
  corrige encore.
- La vue d'ensemble : qui est en service, en pause, pas commencé, terminé.

**Administration**
- Les statuts d'event, désormais calculés depuis les horaires et colorés :
  À venir, En cours, Terminé.
- L'équipe qui s'affiche directement sur la fiche d'un event en cours, sans clic.
- L'interrupteur « Candidatures spontanées », qui permet enfin de fermer un event.
- Le retrait d'une personne assignée, et son refus quand elle a déjà démarré.
- La correction du total de pause à la validation d'une timesheet.
- La traçabilité : qui a démarré ou arrêté le service de qui.

### Captures à refaire

`03-en-service`, `04-en-pause`, `05-reprise`, `06-recap` et `09-equipe-du-jour`
ont changé d'apparence. En ajouter pour le pilotage par le responsable et pour
les statuts colorés.

---

## Vérification qui n'a jamais été faite

Les règles Firestore n'ont jamais été exercées avec de vrais comptes. Les tests
tournent contre `InMemoryDataService`, qui n'applique aucune règle : qu'elles
compilent ne prouve rien sur ce qu'elles autorisent.

Le contrôle décisif tient en un geste : **avec le compte d'un responsable du jour
non-admin, démarrer le service de quelqu'un.** Si ça passe sans message rouge, la
pause, la reprise et l'arrêt suivent — c'est la même règle.

Les trois autres contrôles, dans l'ordre : désigner un responsable en admin ;
vérifier qu'un autre collaborateur assigné se voit refuser `/e/events/{id}/equipe` ;
confirmer dans la console Firestore que le pointage a bien écrit `presence` et
rien d'autre.

Une suite de tests contre l'émulateur Firestore refermerait ce trou pour de bon.
