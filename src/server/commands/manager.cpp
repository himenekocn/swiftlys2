/************************************************************************************************
 * SwiftlyS2 is a scripting framework for Source2-based games.
 * Copyright (C) 2025 Swiftly Solution SRL via Sava Andrei-Sebastian and it's contributors
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 ************************************************************************************************/

#include "manager.h"

#include <api/interfaces/manager.h>

#include <api/shared/string.h>

#include <core/bridge/metamod.h>

#include <public/icvar.h>
#include <s2binlib/s2binlib.h>

std::map<std::string, ConCommand*> conCommandCreated;
std::map<uint64_t, std::string> conCommandMapping;

std::map<std::string, std::function<void(int, std::vector<std::string>, std::string, std::string, bool)>> g_mCommandHandlers;

std::map<uint64_t, std::function<int(int, const std::string&)>> g_mClientCommandListeners;
std::map<uint64_t, std::function<int(int, const std::string&, bool)>> g_mClientChatListeners;

std::set<std::string> commandPrefixes;
std::set<std::string> silentCommandPrefixes;

void DispatchConCommand(void* _this, ConCommandRef cmd, const CCommandContext& ctx, const CCommand& args);
IVFunctionHook* g_pDispatchConCommandHook = nullptr;

void commandsCallback(const CCommandContext& context, const CCommand& args)
{
    CCommand tokenizedArgs;
    tokenizedArgs.Tokenize(args.GetCommandString());

    std::string commandName = tokenizedArgs[0];

    std::transform(commandName.begin(), commandName.end(), commandName.begin(), ::tolower);
    std::string originalCommandName = commandName;
    if (!g_mCommandHandlers.contains(commandName)) commandName = "sw_" + commandName;
    if (!g_mCommandHandlers.contains(commandName)) return;

    std::vector<std::string> argsplit = TokenizeCommand(args.GetCommandString());
    argsplit.erase(argsplit.begin());

    auto& handler = g_mCommandHandlers[commandName];
    handler(context.GetPlayerSlot().Get(), argsplit, originalCommandName, "sw_", true);
}

void CServerCommands::Initialize()
{
    static auto hooksmanager = g_ifaceService.FetchInterface<IHooksManager>(HOOKSMANAGER_INTERFACE_VERSION);
    static auto gamedata = g_ifaceService.FetchInterface<IGameDataManager>(GAMEDATA_INTERFACE_VERSION);

    void* ccvarVTable;
    s2binlib_find_vtable("tier0", "CCvar", &ccvarVTable);

    g_pDispatchConCommandHook = hooksmanager->CreateVFunctionHook();
    g_pDispatchConCommandHook->SetHookFunction(ccvarVTable, gamedata->GetOffsets()->Fetch("ICvar::DispatchConCommand"), (void*)DispatchConCommand, true);
    g_pDispatchConCommandHook->Enable();
}

void CServerCommands::Shutdown()
{
    static auto hooksmanager = g_ifaceService.FetchInterface<IHooksManager>(HOOKSMANAGER_INTERFACE_VERSION);
    if (g_pDispatchConCommandHook)
    {
        g_pDispatchConCommandHook->Disable();
        hooksmanager->DestroyVFunctionHook(g_pDispatchConCommandHook);
        g_pDispatchConCommandHook = nullptr;
    }
}

// @returns 1 - command is not silent
// @returns 2 - command is silent
// @returns -1 - invalid controller
// @returns 0 - is not command
int CServerCommands::HandleCommand(int playerid, const std::string& text)
{
    if (text == "" || text.size() == 0)
        return -1;

    static auto playermanager = g_ifaceService.FetchInterface<IPlayerManager>(PLAYERMANAGER_INTERFACE_VERSION);
    static auto configuration = g_ifaceService.FetchInterface<IConfiguration>(CONFIGURATION_INTERFACE_VERSION);

    IPlayer* player = playermanager->GetPlayer(playerid);
    if (player == nullptr)
        return -1;

    if (commandPrefixes.size() == 0) commandPrefixes = explodeToSet(std::get<std::string>(configuration->GetValue("core.CommandPrefixes")), " ");
    if (silentCommandPrefixes.size() == 0) silentCommandPrefixes = explodeToSet(std::get<std::string>(configuration->GetValue("core.CommandSilentPrefixes")), " ");

    bool isCommand = false;
    bool isSilentCommand = false;
    std::string selectedPrefix = "";

    if (commandPrefixes.size() > 0) {
        for (auto it = commandPrefixes.begin(); it != commandPrefixes.end(); ++it) {
            std::string prefix = *it;
            auto strPrefix = text.substr(0, prefix.size());

            if (prefix == strPrefix) {
                isCommand = true;
                selectedPrefix = prefix;
                break;
            }
        }
    }

    if (!isCommand && silentCommandPrefixes.size() > 0) {
        for (auto it = silentCommandPrefixes.begin(); it != silentCommandPrefixes.end(); ++it) {
            std::string prefix = *it;
            auto strPrefix = text.substr(0, prefix.size());

            if (prefix == strPrefix) {
                isSilentCommand = true;
                selectedPrefix = prefix;
                break;
            }
        }
    }

    if (isCommand || isSilentCommand)
    {
        CCommand tokenizedArgs;
        tokenizedArgs.Tokenize(text.c_str());

        std::vector<std::string> cmdString = TokenizeCommand(text);
        cmdString.erase(cmdString.begin());

        if (tokenizedArgs.ArgC() < 1)
            return 0;

        std::string commandName = tokenizedArgs[0];
        if (commandName.size() < 1)
            return 0;

        commandName.erase(0, selectedPrefix.size());
        std::transform(commandName.begin(), commandName.end(), commandName.begin(), ::tolower);

        std::string originalCommandName = commandName;
        if (!g_mCommandHandlers.contains(commandName)) commandName = "sw_" + commandName;
        if (!g_mCommandHandlers.contains(commandName)) return 0;

        g_mCommandHandlers[commandName](playerid, cmdString, originalCommandName, selectedPrefix, isSilentCommand);
    }

    if (isCommand)
        return 1;
    else if (isSilentCommand)
        return 2;
    else
        return 0;
}

bool CServerCommands::HandleClientCommand(int playerid, const std::string& text)
{
    for (const auto& [id, listener] : g_mClientCommandListeners) {
        auto res = listener(playerid, text);
        if (res == 1) return false;
        else if (res == 2) break;
    }

    return true;
}

bool CServerCommands::HandleClientChat(int playerid, const std::string& text, bool teamonly)
{
    for (const auto& [id, listener] : g_mClientChatListeners) {
        auto res = listener(playerid, text, teamonly);
        if (res == 1) return false;
        else if (res == 2) break;
    }

    return true;
}

uint64_t CServerCommands::RegisterCommand(std::string command_name, std::function<void(int, std::vector<std::string>, std::string, std::string, bool)> handler, bool registerRaw)
{
    std::transform(command_name.begin(), command_name.end(), command_name.begin(), ::tolower);

    if (!registerRaw)
    {
        if (conCommandCreated.contains(command_name))
            return 0;

        command_name = "sw_" + command_name;
    }

    static uint64_t command_id = 0;
    if (!conCommandCreated.contains(command_name)) {
        conCommandCreated[command_name] = new ConCommand(command_name.c_str(), commandsCallback, "SwiftlyS2 registered command", (1 << 25) | (1 << 0) | (1 << 24));
        conCommandMapping[++command_id] = command_name;
        g_mCommandHandlers[command_name] = handler;
    }
    return command_id;
}

void CServerCommands::UnregisterCommand(uint64_t command_id)
{
    auto it = conCommandMapping.find(command_id);
    if (it == conCommandMapping.end()) return;

    std::string command_name = it->second;
    auto it2 = conCommandCreated.find(command_name);
    if (it2 == conCommandCreated.end()) return;

    delete it2->second;

    conCommandCreated.erase(it2);
    conCommandMapping.erase(it);

    auto it3 = g_mCommandHandlers.find(command_name);
    if (it3 != g_mCommandHandlers.end()) g_mCommandHandlers.erase(it3);
}

uint64_t CServerCommands::RegisterAlias(std::string alias_command, std::string command_name, bool registerRaw)
{
    if (!g_mCommandHandlers.contains(command_name)) command_name = "sw_" + command_name;
    auto& handler = g_mCommandHandlers[command_name];
    return RegisterCommand(alias_command, handler, registerRaw);
}

void CServerCommands::UnregisterAlias(uint64_t alias_id)
{
    return UnregisterCommand(alias_id);
}

uint64_t CServerCommands::RegisterClientCommandsListener(std::function<int(int, const std::string&)> listener)
{
    static uint64_t listener_id = 0;
    g_mClientCommandListeners[++listener_id] = listener;
    return listener_id;
}

void CServerCommands::UnregisterClientCommandsListener(uint64_t listener_id)
{
    g_mClientCommandListeners.erase(listener_id);
}

uint64_t CServerCommands::RegisterClientChatListener(std::function<int(int, const std::string&, bool)> listener)
{
    static uint64_t listener_id = 0;
    g_mClientChatListeners[++listener_id] = listener;
    return listener_id;
}

void CServerCommands::UnregisterClientChatListener(uint64_t listener_id)
{
    g_mClientChatListeners.erase(listener_id);
}

void DispatchConCommand(void* _this, ConCommandRef cmd, const CCommandContext& ctx, const CCommand& args)
{
    CPlayerSlot slot = ctx.GetPlayerSlot();
    static auto servercommands = g_ifaceService.FetchInterface<IServerCommands>(SERVERCOMMANDS_INTERFACE_VERSION);
    static auto playermanager = g_ifaceService.FetchInterface<IPlayerManager>(PLAYERMANAGER_INTERFACE_VERSION);
    static auto eventmanager = g_ifaceService.FetchInterface<IEventManager>(GAMEEVENTMANAGER_INTERFACE_VERSION);

    if (slot.Get() != -1)
    {
        if (!servercommands->HandleClientCommand(slot.Get(), args.GetCommandString())) return;

        std::string command = args.Arg(0);
        if (command == "say" || command == "say_team")
        {
            auto player = playermanager->GetPlayer(slot.Get());
            if (!player) return;

            void* controller = player->GetController();
            bool teamonly = (command == "say_team");

            auto text = args[1];
            if (strlen(text) == 0) return;

            if (controller)
            {
                IGameEvent* pEvent = eventmanager->GetGameEventManager()->CreateEvent("player_chat");

                if (pEvent)
                {
                    pEvent->SetBool("teamonly", teamonly);
                    pEvent->SetInt("userid", slot.Get());
                    pEvent->SetString("text", text);

                    eventmanager->GetGameEventManager()->FireEvent(pEvent, true);
                }
            }

            int handleCommandReturn = servercommands->HandleCommand(slot.Get(), text);
            if (handleCommandReturn == 2 || !servercommands->HandleClientChat(slot.Get(), text, teamonly)) return;
        }
    }

    return reinterpret_cast<decltype(&DispatchConCommand)>(g_pDispatchConCommandHook->GetOriginal())(_this, cmd, ctx, args);
}