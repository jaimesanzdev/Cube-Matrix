# Group Name: Hollow Face Studios

# Game Description:
The gameplay loop of Cube-Matrix will be inspired by the game Bloxorz, with modifications and improvements to make the gameplay more complicated and in line with project expectations. The game will be a mobile game where the player controls the cube by swiping and tapping the touchscreen, allowing for movement in the four cardinal directions by rotating and flipping the cube. The main goal is to traverse the map to reach the specified end tile. The twist is that the cube can only shoot projectiles or interact with portals, elevators, doors, and the goal tile with one specific hollowed face. This makes it so the player must be very intentional with their movement and plan actions ahead of time, or else they cannot finish the level and must restart. The interactable objects integrated within the map design will make use of vertex shaders to make the game more visually stimulating, in addition to our custom-made voxel assets.

The core systems driving the game are a discrete tile-based movement system, a cube orientation tracker that knows which face is pointing in which direction at all times, and a modular interaction system connecting buttons to doors, elevators, and moving platforms. The hollow face mechanic is the central design constraint — it prevents the player from pressing buttons or completing the win condition unless the cube is oriented correctly, adding a layer of spatial reasoning to every move.

For class elements, we are incorporating custom vertex shaders on interactable objects and the cube itself, voxel-style 3D assets, mobile touch input, and a clean Git-based collaborative workflow with individual branches per team member. The X-Factor elements we are targeting are mobile support and custom shaders.

# Group Organization:
- Jaime Sanz Villalba: Lead Developer — core gameplay systems, cube movement, level design
- Mikah Martinez: Gameplay Developer — cube orientation system, win condition, face interaction logic
- Simmon Quan: Gameplay Developer — interactable mechanics (buttons, elevators, doors), map design
- Hannah: Art & Shader Developer — voxel asset creation, visual design, custom vertex shaders
- Kane: Audio & QA — sound design, SFX, mobile game testing





