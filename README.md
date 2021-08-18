ScriptParasite
==============

When enabled, the script parasite component watches the C# component it’s grouped with, and writes the changes to the file [Nickname].cs in the selected folder. By default this is the folder “GrasshopperScripts” in my documents.

#### Installation

- Rhino 6 using Package manager: using the command `TestPackageManager`, and select ScriptParasite
- Rhino 7 using Package manager: Type the command `PackageManager`, and select ScriptParasite
- Manual installation for all versions: Download the latest .zip file, and place in rhino's plugin and grasshopper component folder.

#### Getting started

1. Add the script component in a group with the C# component you want to edit
2. Set enabled to true
3. Head over to your My Documents\GrasshopperScripts
4. With Visual Studio Code: Open the folder.
5. With Visual Studio Community or Rider, open the GrasshopperScripts.csproj file.
6. That’s it!

#### Support
For support regarding this plugin, please do not use the comments below, but use the grasshopper and rhino forum at discourse.mcneel.com with the tag scriptparasite.

For bugs, please use the github issue tracker

#### Requirements

    An IDE. If you have none installed I recommend you Visual Studio Code (free, also works for the Mac)
    In Rhino make sure COFF loading is disabled, or set the loading mechanism for the ScriptComponents is set to Disk in "File > Preferences > Solver"

#### Folders
The folder that is last successfully written to will be saved as the default export folder for all future times you use the component.

If the folder does not contain a .csproj file (used by Visual Studio, Visual Studio code, and all C# ide’s), a new csproj file is created for you with the correct references to Rhino and Grasshopper.

#### Watching for changes
The following changes to the component are synced and written to the C# file:

    Added parameters (input/output)
    Changed parameter name (input/output)
    Changed parameter type and list type (input)

#### IDE’s/Editors
I’ve tested with Visual Studio 2017 (needs a bit of a recent update to work with the new csproj format), Visual Studio Code, and Rider.

#### Grasshopper for Mac
This plugin is reported to have been working for mac.

#### Source code / Licence / Contributing
The plugin is open source (MIT Licence) and available on Github. If you have suggestions or improvements, throw me an email, or send an issue/pull request on github, and I'll get back to you.

#### Version history
2021-08-18, Version 1.1.0: Fixed whitespace issues, line numbers are now matching the line numbers in the editor. Solved Visual Studio and Visual Studio Code problems, improved robustness. Dropped Rhino 5 / grasshopper 0.9.x support.
2020-11-06, Version 1.0.4: Update for mac: no longer adding a backslash before the filename
2019-03-12, Version 1.0.1: Now adding importing and exporting namespaces in the custom namespaces area.
2018-12-21, Version 1.0.0: Initial release

#### Credits
- [Andrew Heumann], [Anton Kerezov], [Zac Zhang] for kindly contributing code improvements

#### Contact
For any issues, kindly ask on the discourse forum of McNeel, or open an issue on github.

[Andrew Heumann]:https://github.com/andrewheumann
[Anton Kerezov]:[https://github.com/dilomo]
[Zac Zhang]:[https://github.com/ZacZhangzhuo]