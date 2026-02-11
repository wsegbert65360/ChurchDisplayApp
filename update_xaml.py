import re

# Read the XAML file
xaml_path = r"c:\Projects\ChurchDisplayApp\ServiceElementsWindow.xaml"
with open(xaml_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Define the mappings of element keys
elements = [
    ("SongForBeginning", "SongForBeginning"),
    ("CallToWorship", "CallToWorship"),
    ("PraiseSong", "PraiseSong"),
    ("GloriaPatri", "GloriaPatri"),
    ("LordsPrayer", "LordsPrayer"),
    ("PrayerSong", "PrayerSong"),
    ("ChildrensMomentSong", "ChildrensMomentSong"),
    ("CommunionSong", "CommunionSong"),
    ("Doxology", "Doxology"),
    ("InvitationSong", "InvitationSong"),
    ("EndingSong", "EndingSong")
]

# Replace Select handlers
for element_key, _ in elements:
    # Replace Select Click handlers
    old_select = f'Click="{element_key}Select_Click"'
    new_select = f'Click="ElementSelect_Click" Tag="{element_key}"'
    content = content.replace(old_select, new_select)
    
    # Replace Clear Click handlers
    old_clear = f'Click="{element_key}Clear_Click"'
    new_clear = f'Click="ElementClear_Click" Tag="{element_key}"'
    content = content.replace(old_clear, new_clear)
    
    # Replace Use Click handlers (if any exist)
    old_use = f'Click="{element_key}Use_Click"'
    new_use = f'Click="ElementUse_Click" Tag="{element_key}"'
    content = content.replace(old_use, new_use)

# Write back
with open(xaml_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("XAML file updated successfully")
