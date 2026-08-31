# Prompt-Verlauf der Applikationsentwicklung

Diese Datei enthält ausschließlich die Eingaben des Benutzers aus dem Chat, in chronologischer Reihenfolge. Antworten des Assistenten sowie interne System- und Werkzeugmeldungen sind nicht enthalten. Schreibweise und Formulierungen wurden weitgehend unverändert übernommen; lediglich die Markdown-Formatierung wurde vereinheitlicht.

- untersuche diese website und mache einen plan wie ich diese website nachbauen könnte

  woher kommen die Daten

  wie könnte man die schweiz mit den verschieden Farben darstellen

  für erste keine gps funktionen

  stelle fragen

  https://meteoradar.ch/regenkarten/

- lieber in asp.net

  betreiben in azure als web app service

  infrastruktur mit terraform

  weitere Fragen

- > Wie zuverlässig muss die Aktualisierung alle fünf Minuten laufen?

  Nur aktualisieren, wenn die Website läuft

  > Welche Daten sollen in Azure gespeichert werden?

  Kartenbilder und 14 Tage Rohdaten

  > Wer soll die Webanwendung verwenden können?

  Öffentlich ohne Anmeldung

- > Wie eigenständig soll die Gestaltung gegenüber meteoradar.ch sein?

  Modernes eigenes Design

  > Welche Bedienfunktionen soll die erste Version enthalten?

  Zeitraum auswählen

  > Wie soll die Anwendung bereitgestellt werden?

  GitHub Actions

- > Soll die Schweizkarte in Version 1 fest stehen oder weiterhin zoombar sein?

  Zoomen und Verschieben

  > Welche Regensummen sollen auswählbar sein?

  1, 3, 6, 12 und 24 Stunden

  > Wie soll GitHub Actions auf Azure zugreifen?

  Noch nicht entschieden

- kannst alle generierten sachen in einem weiteren github repo speichern für user ezeh2

- bist du vorher steckengeblieben

- did you get stuck while installing net 10

- erstellt

- So, ich möchte dich ein paar Sachen fragen zu der eben generierten Applikation.

- Diese Unit-Tests, sind das nur wirklich Unit-Tests oder sind das auch End-to-End-Tests, die du da geschrieben hast?

- Diese ASP.NET Applikation, die lädt ja auch Daten runter von Swiss Meteo, glaube ich. Wie werden diese Daten gespeichert und von wo werden sie gespeichert? Werden sie einfach auf dem Dateisystem gespeichert, wo auch die ASP.NET Applikation läuft?

- Wie erfolgt diese Umschaltung zwischen lokal gespeicherten Daten auf lokaler Entwicklungsumgebung und das Speichern im Blob Storage in der Azure Produktivumgebung?

- Muss ich irgendwie einen Account, speziellen Account haben bei Swiss Meteo oder irgendwas Spezielles bezahlen, damit ich da Daten runterladen darf? Oder ist das einfach frei für jedermann ohne speziellen Account?

- Was bedeutet die Abkürzung STAC? Und sind diese Daten im JSON-Format oder werden die in einem Binary-Format runtergeladen von SwissMeteo?

- Du hast geschrieben, dass alle fünf Minuten Daten runtergeladen werden von SwissMeteo. Das heißt, da ist irgendein Cron-Job eingerichtet worden in ASP.NET. Kannst du mir Details dazu sagen, wie dieser Cron-Job funktioniert?

- Wenn ich diese ASP.NET Applikation lokal laufen lasse, dann weiß ich jetzt, dieser Background Worker lädt alle fünf Minuten Files runter, die noch nicht lokal vorhanden sind. Aber diese ASP.NET Applikation liefert die auch Webseiten? Oder sind diese Webseiten woanders gespeichert?

- Was ist die Leaflet-Bibliothek? Kannst du mir das im Detail erklären?

- Wenn ich jetzt diese Applikation lokal laufen lasse, also ich weiß, die Applikation ist kompilierbar, du hast das sicher durchgetestet, dass es kompiliert. Die Unit-Tests funktionieren, aber ob die Applikation wirklich das macht, was du dir vorgestellt hast, das wissen wir noch nicht. Das müsste ich jetzt selbst austesten.

- Was mich jetzt überrascht: Du hast diese Applikation wirklich laufen lassen in deiner eigenen Umgebung, hast da schon auch gewisse Tests durchgeführt. Also eigentlich hast du manuell End-to-End-Tests durchgeführt.

- Das Package.json, das im Root liegt, hast du verwendet, um da dieses NPM-Package Leaflet runterzuladen. Aber das ist eine Bibliothek, die nur clientseitig läuft. Das heißt, da sind nur JavaScript-Files drin, vermute ich, und serverseitig läuft da nichts. Ist das richtig so?

- Ich möchte noch besser verstehen, was dieses npm run vendor macht. Ich weiß, Node oder npm, dass sie dieses File, dieses JSON-File ausführen sollen, dieses vendor-strich assets.mjs.

- All diese Fragen, die ich dir jetzt gestellt habe, wie könnte man das dokumentieren im bestehenden Repository? Mache einen Vorschlag für eine Ablage einer solchen Dokumentation.

- ok do it and create all the proposed documents

- Siehst du eine Möglichkeit, aus diesem Chat den ganzen Text rauszukopieren, den ich eingegeben habe? Weil ich möchte nachträglich zeigen, wie wenig es braucht, um eine solche Applikation mit deiner Unterstützung zu bauen. Einfach am besten wäre einfach ein MD-File, wo einfach als Bullet-List all die Sachen, die Texte, die Fragen reinschreibt, die ich gestellt habe in diesem Chatverlauf.
