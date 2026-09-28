# Triage Labels

The skills speak in terms of five canonical triage roles. This file maps those roles to the actual label strings used in this repo's issue tracker.

This repo uses a `status:`-prefixed vocabulary, the convention common to serious public repos (Vue, Vite, Nuxt, Astro). `wontfix` is deliberately left unprefixed: GitHub creates it by default in every new repo, so prefixing it would create a duplicate.

| Label in mattpocock/skills | Label in our tracker     | Meaning                                                                       |
| -------------------------- | ------------------------ | ----------------------------------------------------------------------------- |
| `needs-triage`             | `status: needs-triage`   | Maintainer needs to evaluate this issue                                        |
| `needs-info`               | `status: needs-info`     | Waiting on reporter for more information                                       |
| `ready-for-agent`          | `status: ready`          | Fully specified and self-contained — anyone, or an AFK agent, can pick it up   |
| `ready-for-human`          | `status: needs-maintainer` | Requires human judgment or maintainer context                                 |
| `wontfix`                  | `wontfix`                | Will not be actioned (GitHub default label — do not recreate)                  |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), use the corresponding label string from this table.

Edit the right-hand column to match whatever vocabulary you actually use.

## Creating these labels

Four of the five do not exist in a fresh GitHub repo. Create them once:

```bash
gh label create "status: needs-triage"     --color FBCA04 --description "Maintainer needs to evaluate this issue"
gh label create "status: needs-info"       --color D4C5F9 --description "Waiting on reporter for more information"
gh label create "status: ready"            --color 0E8A16 --description "Fully specified and self-contained; ready to be picked up"
gh label create "status: needs-maintainer" --color 1D76DB --description "Requires human judgment or maintainer context"
```
