# Changelog

## 0.2.2

### Debuffs on health bars
- Enemy nameplates now show your debuffs under the health bar (Burning, Bleed, Poison, Decay, Chill, Static and the rest) with their stack counts, instead of elite affixes.
- The mini-boss and boss bar shows your debuffs too. Each icon darkens clockwise as its time runs out.
- Elite affixes are explained in a new Affixes tab at the Spellbook Keeper.
- Damage over time effects no longer fall off right after a level-up choice.

### Combat
- Bosses no longer hit you from far away. Enemy melee reach is capped (Magmadar reached almost 15 yards).
- Mini-bosses are tougher: 35 times a normal enemy's health, up from 14.
- An elite that is a bounty target no longer also drops its own chest.
- Level-ups add the max health you gain on top of the 5% heal.
- XP orbs fly to your hero's chest, and follow you through the air.
- The kodo caravan stops while the run is paused.

### Paladin
- Hammer of Justice only knocks down enemies it stuns. Bosses, mini-bosses and stun-immune enemies no longer plays the stun animation when they're not stunned.
- Avenger's Shield no longer looks like it knocks enemies down.
- Judgement plays your cast animation and an impact on each target.

### Sound
- XP pickups play a sound for each orb. It can be turned off in Sound Options (Experience Pickup Sounds).
- Valor Pouch pickups play the coin sound.

### Game window
- New proper Borderless window mode, on by default: the game fills your screen without a title bar or borders, like modern games. Alt+Tab switches instantly, and the picture stays sharp with Windows display scaling.
- Fixed a stretched picture on ultrawide and other screens when the game resolution didn't match the screen's shape. The launcher now sets your screen's resolution when the two don't match.
- Window mode now opens as a normal window that fits above the taskbar.

### Other changes
- Upgrade offers name the reward type (Darkmoon Card, upgrade, reward or blessing), and cards grow to fit long text.
- Shorter Darkmoon Card descriptions.
- Hero creation now says races are cosmetic only, with no racial bonuses.

## 0.2.1

### Gear
- Gear can now be looted as well. Carry the piece in your bags to either wear it later or extract it. Found gear is still only saved if it's in the Extraction Vault.
- You can change gear from your bags during a run. Putting on a shield or off-hand while holding a two-hander moves the two-hander to your bags.

### Hearthstone
- A Hearthstone button sits next to the hotbar while you carry one, used on T by default (Key Bindings, Use Hearthstone). Your first one found comes with a short explanation.
- The Hearthstone's Extraction Vault holds 1 item, even with Pack Mule.

### Controller
- Moving, jumping and buttons work as soon as you enter the world, without touching the mouse or keyboard first.
- The camera on new installs no longer swings behind your hero on its own, which made movement feel reversed and the camera sluggish.

### Other changes
- Your health now shows as numbers on your health bar. On by default, turn it off in Interface Options (Health Numbers).
- Fixed a rare login error ("The information you have entered is not valid") when starting the game.

## 0.2.0

### Paladin
- Paladin is now available. Still WIP like every class.
- 13 spells, 26 evolutions, three talent trees (Holy, Protection, Retribution) each with its own capstone, and 10 Paladin quests that award talent points.

### Other changes
- You can now customize your character when you create it: skin color, face, hairstyle, hair color and facial hair.
- Quest unlocks no longer need a Spellbook purchase.
- Addons other than Hordes of Azeroth are turned off when the launcher starts the game.
- Enemy warnings and bolts vanish when the enemy casting them dies.
- Spell cooldowns on the hotbar pause while a menu, shop or reward choice is open.
- The tutorial explains Gold, Darkmoon Tickets and Valor.
- Launcher fixes for screen resolution and database setup.
- No more going Away and sitting down when idle, no honor restart countdown and no tutorial popups from the base game.
- Ironman mode: tick the box when you create a character, and that character keeps its own gold and unlocks, apart from your other characters.

### Balance

#### Warrior
- Blood Aura, Aegis and Spiked Shield reach ~~6~~ → **8** yards.
- Bloodthirst no longer loses most of its damage due to a scaling bug early in a run.

#### Priest
- Halo also hits enemies right next to you, pulses every ~~5~~ → **4** sec and heals up to ~~5%~~ → **2%** of your health.
- Smite hits ~~1~~ → **2** enemies. Chastise every ~~20~~ → **12** sec.
- Penance +30% damage, Mind Blast +20%.

#### Rogue
- Sinister Strike and Ambush +25% damage. Killing Spree every ~~12~~ → **8** sec.
- Killing Spree shows a slash on every enemy it hits.
- Shuriken Toss throws ~~2~~ → **3** shurikens, +25% damage.

#### Shaman
- Lightning Bolt bounces to a second enemy, +60% damage.
- Chain Lightning and Forked Lightning jump to ~~3~~ → **4** enemies. Chain Lightning every ~~2.5~~ → **3** sec, +70% damage.
- Flame Shock and Earth Shock +50% damage. Windfury Weapon, Stormstrike and Magma Totem +33%.

## 0.1.1

- Monster paths now come as a download instead of being built on your processor. Installs and updates fetch them in a few minutes (about 330 MB), with no more long wait at 100% CPU. If your launcher already finished building them, there's nothing extra to download.
- If the download doesn't work, the launcher builds them on your PC like before. It first asks whether to use all of your processor cores or half, so your PC stays usable, and you can change that later from the ••• menu next to Play.
- Building monster paths now runs at a lower priority, so the rest of your PC stays responsive.

## 0.1.0

The first public version. It's early, so here's what to expect.

- Balance hasn't had much work yet. Some classes, spells and builds are bound to be too strong or too weak, and some zones are harder than they should be. Tell us what feels off.
- Six classes are playable. Druid, Paladin and Warlock come in later updates.
- Expect bugs. If something breaks, use Report a bug in the launcher: it saves a zip of your logs on your desktop to attach, or post it in the Discord.
- The first install takes a while, because the launcher builds the monster paths from your own game files. The harder zones keep building in the background while you play.
- Updates come through the launcher. Your characters are backed up before every update and your unlocks stay.
