# Modèle de données proposé

## Principe

Utiliser un **modèle hybride** :

- Structure commune en base relationnelle.
- Configuration spécifique de chaque élément en `jsonb`.
- État d'exécution séparé de la configuration.

---

## Tables

### Presentation

| Champ | Description |
|-------|-------------|
| Id | Identifiant |
| Title | Titre |

### Slide

| Champ | Description |
|-------|-------------|
| Id | Identifiant |
| PresentationId | Présentation |
| Order | Ordre |
| BackgroundColor | Couleur de fond |

### Element

| Champ | Description |
|-------|-------------|
| Id | Identifiant |
| SlideId | Slide |
| Type | text, image, quiz... |
| X | Position X |
| Y | Position Y |
| Width | Largeur |
| Height | Hauteur |
| Rotation | Rotation |
| ZIndex | Couche |
| Locked | Verrouillé |
| Visible | Visible |
| Configuration (jsonb) | Données propres au composant |
| CreatedAt | Création |
| UpdatedAt | Modification |

---

## Exemples de Configuration

### Texte

```json
{
  "content": "Bonjour",
  "fontSize": 24,
  "fontFamily": "Inter",
  "color": "#000000"
}
```

### Image

```json
{
  "assetId": "image-123",
  "fit": "contain"
}
```

### Quiz

```json
{
  "question": "2 + 2 ?",
  "answers": ["3", "4", "5"],
  "correct": 1
}
```

---

## Runtime

Ne pas stocker l'état d'une présentation dans `Configuration`.

Créer un état d'exécution séparé (en mémoire ou dans une table dédiée selon le besoin).

Exemples :

- Présentation démarrée
- Élèves connectés
- Quiz ouvert / fermé
- Réponses des élèves
- Timer en cours

---

## Architecture des éléments

Chaque élément est un plugin.

Il fournit :

- Un composant d'édition
- Un composant de rendu
- Un panneau de propriétés
- Un schéma de validation
- Les événements temps réel (SignalR)

Le moteur manipule uniquement des `Element`.

---

## Avantages

- Très peu de migrations SQL.
- Ajout d'un nouveau widget sans modifier le modèle.
- Requêtes SQL simples sur les éléments.
- Configuration flexible grâce au `jsonb`.
- Synchronisation WebSocket/SignalR élément par élément.
- Architecture extensible et maintenable.
