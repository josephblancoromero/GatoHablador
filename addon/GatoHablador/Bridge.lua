local ADDON_NAME = ...
local MAGIC = 0xA7
local VERSION = 1
local FRAME_BYTES = 64
local HEADER_BYTES = 6
local PAYLOAD_BYTES = 56
local COLUMNS = 64
local ROWS = 8
local CELL_SIZE = 2
local FRAME_SECONDS = 0.18

local band, bxor, lshift = bit.band, bit.bxor, bit.lshift
local bridge = CreateFrame("Frame", "GatoHabladorBridge", UIParent)
bridge:SetSize(COLUMNS * CELL_SIZE, ROWS * CELL_SIZE)
bridge:SetPoint("TOPLEFT", UIParent, "TOPLEFT", 0, 0)
bridge:SetFrameStrata("TOOLTIP")
bridge:SetFrameLevel(9999)
bridge:EnableMouse(false)

local function ApplyPixelScale()
    local scale = UIParent:GetEffectiveScale()
    if scale and scale > 0 then bridge:SetScale(1 / scale) end
end

ApplyPixelScale()

local cells = {}
for index = 1, COLUMNS * ROWS do
    local texture = bridge:CreateTexture(nil, "OVERLAY")
    local x = (index - 1) % COLUMNS
    local y = math.floor((index - 1) / COLUMNS)
    texture:SetPoint("TOPLEFT", bridge, "TOPLEFT", x * CELL_SIZE, -(y * CELL_SIZE))
    texture:SetSize(CELL_SIZE, CELL_SIZE)
    texture:SetColorTexture(0, 0, 0, 1)
    cells[index] = texture
end

local queue = {}
local activeFrame = nil
local elapsed = 0
local sequence = 0

local function Print(message)
    DEFAULT_CHAT_FRAME:AddMessage("|cff55d6beGatoHablador:|r " .. message)
end

local function Escape(value)
    value = tostring(value or "")
    value = value:gsub("%%", "%%25")
    value = value:gsub("\t", "%%09")
    value = value:gsub("\r", "%%0D")
    value = value:gsub("\n", "%%0A")
    return value
end

local function Crc16(bytes)
    local crc = 0xFFFF
    for index = 1, 62 do
        crc = bxor(crc, lshift(bytes[index] or 0, 8))
        for _ = 1, 8 do
            if band(crc, 0x8000) ~= 0 then
                crc = band(bxor(lshift(crc, 1), 0x1021), 0xFFFF)
            else
                crc = band(lshift(crc, 1), 0xFFFF)
            end
        end
    end
    return crc
end

local function NewFrame(chunkIndex, chunkCount, payload)
    local bytes = {}
    for index = 1, FRAME_BYTES do bytes[index] = 0 end
    bytes[1] = MAGIC
    bytes[2] = VERSION
    bytes[3] = sequence
    bytes[4] = chunkIndex
    bytes[5] = chunkCount
    bytes[6] = #payload
    for index = 1, #payload do bytes[HEADER_BYTES + index] = payload:byte(index) end
    local crc = Crc16(bytes)
    bytes[63] = math.floor(crc / 256)
    bytes[64] = crc % 256
    return bytes
end

local function Render(bytes)
    for byteIndex = 1, FRAME_BYTES do
        local value = bytes and bytes[byteIndex] or 0
        for bitIndex = 0, 7 do
            local enabled = band(value, lshift(1, 7 - bitIndex)) ~= 0
            local cell = cells[((byteIndex - 1) * 8) + bitIndex + 1]
            if enabled then cell:SetColorTexture(1, 1, 1, 1)
            else cell:SetColorTexture(0, 0, 0, 1) end
        end
    end
end

local function Enqueue(eventName, author, text)
    if not GatoHabladorDB.enabled then return end
    local wire = Escape(eventName) .. "\t" .. Escape(author) .. "\t" .. Escape(text)
    local chunkCount = math.max(1, math.ceil(#wire / PAYLOAD_BYTES))
    if chunkCount > 255 then return end
    sequence = (sequence + 1) % 256
    for chunkIndex = 0, chunkCount - 1 do
        local first = (chunkIndex * PAYLOAD_BYTES) + 1
        local payload = wire:sub(first, first + PAYLOAD_BYTES - 1)
        queue[#queue + 1] = NewFrame(chunkIndex, chunkCount, payload)
    end
end

bridge:SetScript("OnUpdate", function(_, delta)
    elapsed = elapsed + delta
    if activeFrame and elapsed < FRAME_SECONDS then return end
    elapsed = 0
    activeFrame = table.remove(queue, 1)
    Render(activeFrame)
end)

local chatEvents = {
    "CHAT_MSG_SAY", "CHAT_MSG_YELL", "CHAT_MSG_WHISPER",
    "CHAT_MSG_PARTY", "CHAT_MSG_PARTY_LEADER",
    "CHAT_MSG_RAID", "CHAT_MSG_RAID_LEADER", "CHAT_MSG_RAID_WARNING",
    "CHAT_MSG_INSTANCE_CHAT", "CHAT_MSG_INSTANCE_CHAT_LEADER",
    "CHAT_MSG_GUILD", "CHAT_MSG_OFFICER", "CHAT_MSG_CHANNEL",
}

local events = CreateFrame("Frame")
events:RegisterEvent("ADDON_LOADED")
events:RegisterEvent("UI_SCALE_CHANGED")
events:SetScript("OnEvent", function(_, eventName, text, author)
    if eventName == "UI_SCALE_CHANGED" then
        ApplyPixelScale()
        return
    end
    if eventName == "ADDON_LOADED" then
        if text ~= ADDON_NAME then return end
        GatoHabladorDB = GatoHabladorDB or {}
        if GatoHabladorDB.enabled == nil then GatoHabladorDB.enabled = true end
        for _, chatEvent in ipairs(chatEvents) do pcall(events.RegisterEvent, events, chatEvent) end
        bridge:SetShown(GatoHabladorDB.enabled)
        Print("puente " .. (GatoHabladorDB.enabled and "activo" or "pausado") .. ". Usa /gato status.")
        return
    end
    Enqueue(eventName, author, text)
end)

SLASH_GATOHABLADOR1 = "/gatohablador"
SLASH_GATOHABLADOR2 = "/gato"
SlashCmdList.GATOHABLADOR = function(command)
    command = (command or ""):lower():match("^%s*(.-)%s*$")
    if command == "on" then
        GatoHabladorDB.enabled = true
        bridge:Show()
        Print("puente activo.")
    elseif command == "off" then
        GatoHabladorDB.enabled = false
        queue = {}
        activeFrame = nil
        Render(nil)
        bridge:Hide()
        Print("puente pausado.")
    elseif command == "test" then
        Enqueue("CHAT_MSG_SAY", UnitName("player") or "Jugador", "Need healer")
        Print("mensaje de prueba enviado al puente.")
    else
        Print("estado: " .. (GatoHabladorDB.enabled and "activo" or "pausado") .. ". Comandos: /gato on, /gato off, /gato test")
    end
end

Render(nil)

