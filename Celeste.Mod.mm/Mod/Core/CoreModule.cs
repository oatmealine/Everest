﻿using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    class CoreModule : EverestModule {

        public CoreModule() {
            Metadata = new EverestModuleMetadata() {
                Name = "Everest",
                Version = Everest.Version
            };
        }

        public override void Load() {
            Everest.Events.OuiMainMenu.OnCreateMainMenuButtons += CreateMainMenuButtons;
            Everest.Events.Level.OnCreatePauseMenuButtons += CreatePauseMenuButtons;

        }

        public override void Unload() {
            Everest.Events.OuiMainMenu.OnCreateMainMenuButtons -= CreateMainMenuButtons;
            Everest.Events.Level.OnCreatePauseMenuButtons -= CreatePauseMenuButtons;

        }

        public void CreateMainMenuButtons(OuiMainMenu menu, List<MenuButton> buttons) {
            int index;

            // Find the options button and place our button below it.
            index = buttons.FindIndex(_ => {
                MainMenuSmallButton other = (_ as MainMenuSmallButton);
                if (other == null)
                    return false;
                return other.GetLabelName() == "menu_options" && other.GetIconName() == "menu/options";
            });
            if (index != -1)
                index++;
            // Otherwise, place it above the exit button.
            else
                index = buttons.Count - 1;

            buttons.Insert(index, new MainMenuSmallButton("menu_modoptions", "menu/modoptions", menu, Vector2.Zero, Vector2.Zero, () => {
                Audio.Play("event:/ui/main/button_select");
                Audio.Play("event:/ui/main/whoosh_large_in");
                menu.Overworld.Goto<OuiModOptions>();
            }));
        }

        public void CreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
            List<TextMenu.Item> items = menu.GetItems();
            int index;

            // Find the options button and place our button below it.
            string cleanedOptions = Dialog.Clean("menu_pause_options", null);
            index = items.FindIndex(_ => {
                TextMenu.Button other = (_ as TextMenu.Button);
                if (other == null)
                    return false;
                return other.Label == cleanedOptions;
            });
            if (index != -1)
                index++;
            // Otherwise, place it below the last button.
            else
                index = items.Count;

            TextMenu.Item itemModOptions = null;
            menu.Insert(index, itemModOptions = new TextMenu.Button(Dialog.Clean("menu_pause_modoptions", null)).Pressed(() => {
                int returnIndex = menu.IndexOf(itemModOptions);
                menu.RemoveSelf();
                
                level.Paused = true;

			    TextMenu options = OuiModOptions.CreateMenu(true, LevelExt.PauseSnapshot);

			    options.OnESC = options.OnCancel = () => {
				    Audio.Play("event:/ui/main/button_back");
				    IEnumerator routine = UserIO.SaveHandler(false, true);
				    options.CloseAndRun(routine, () => level.Pause(returnIndex, minimal, false));
			    };

			    options.OnPause = () => {
				    Audio.Play("event:/ui/main/button_back");
				    IEnumerator routine = UserIO.SaveHandler(false, true);
				    options.CloseAndRun(routine, () => {
                        level.Paused = false;
                        Engine.FreezeTimer = 0.15f;
                    });
			    };

			    level.Add(options);
            }));
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            menu.Add(new TextMenu.SubHeader("Everest Experiments"));

            // TODO: EverestModuleSettings
            menu.Add(new TextMenu.OnOff("Rainbow Mode", Everest.Experiments.RainbowMode).Change(v => Everest.Experiments.RainbowMode = v));
        }

    }
}
