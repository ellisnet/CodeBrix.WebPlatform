================================================================================
UPGRADE NOTES: serde and serde_json (JSON serialisation)
================================================================================

Read the generic procedure, UPGRADING A VENDORED LIBRARY in the
MAINTAINER-README.txt at the repository root, first. This file holds what is
particular to these two libraries.

WHERE THEY COME FROM
--------------------
The `serde` and `serde_json` crates on crates.io, from
https://github.com/serde-rs/serde and https://github.com/serde-rs/json.
Both are licensed MIT OR Apache-2.0. Not patched.

HOW THEY ARE USED
-----------------
The generated request and response types derive Serialize / Deserialize
(the `derive` feature of serde), and serde_json reads and writes the JSON
bodies. The backend writes camelCase property names, so the types carry
#[serde(rename_all = "camelCase")].

WHAT THEY ARE TIED TO
---------------------
Dioxus and reqwest depend on both. The platform's manifests (the shared
crate's and the crate set's) name them with loose version requirements ("1")
so cargo keeps a single copy of each at whatever version the rest of the tree
resolves to.

HOW TO UPGRADE
--------------
There is nothing to do on their own: they move when the frontend is
re-resolved for a Dioxus upgrade. Keep the version requirements loose.

================================================================================
END OF UPGRADE NOTES
================================================================================
