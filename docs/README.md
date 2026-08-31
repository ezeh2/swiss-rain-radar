# Documentation

This directory contains the technical documentation for Swiss Rain Radar.

| Document | Purpose |
|---|---|
| [Architecture decisions](architecture.md) | The important architectural choices and scale-out boundary |
| [How the application works](how-it-works.md) | End-to-end explanation of data discovery, processing, storage, APIs and the browser map |
| [Local development](local-development.md) | Prerequisites, startup commands, local storage and troubleshooting |
| [Testing strategy](testing.md) | Existing tests, verified behavior and the remaining integration and end-to-end work |
| [Source-site analysis](source-site-analysis.md) | Analysis of meteoradar.ch and selection of a public replacement data source |
| [User prompt history (German)](user-prompt-history.de.md) | Chronological record of the user inputs that led to the application and its documentation |

## Recommended reading order

New contributors should start with [How the application works](how-it-works.md), follow the setup in [Local development](local-development.md), and then read [Testing strategy](testing.md) before changing data processing or deployment behavior.

The root [README](../README.md) remains the concise project and deployment overview. These pages contain the implementation details that would otherwise make the root README difficult to navigate.
