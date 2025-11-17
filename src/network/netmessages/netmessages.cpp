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

#include "netmessages.h"

#include <api/interfaces/manager.h>
#include <api/sdk/serversideclient.h>
#include <memory/gamedata/manager.h>

#include <api/shared/plat.h>
#include <s2binlib/s2binlib.h>

#include <map>

std::map<uint64_t, std::function<int(uint64_t*, int, void*)>> g_mServerMessageSendCallbacks;
std::map<uint64_t, std::function<int(int, int, void*)>> g_mClientMessageSendCallbacks;

IFunctionHook* g_pFilterMessageHook = nullptr;
IVFunctionHook* g_pPostEventAbstractHook = nullptr;

bool bypassPostEventAbstractHook = false;

bool FilterMessage(INetworkMessageProcessingPreFilterCustom* client, CNetMessage* cMsg, INetChannel* netchan);
void PostEventAbstractHook(void* _this, CSplitScreenSlot nSlot, bool bLocalOnly, int nClientCount, const uint64* clients,
    INetworkMessageInternal* pEvent, const CNetMessage* pData, unsigned long nSize, NetChannelBufType_t bufType);

void CNetMessages::Initialize()
{
    static auto hooksmanager = g_ifaceService.FetchInterface<IHooksManager>(HOOKSMANAGER_INTERFACE_VERSION);
    static auto gamedata = g_ifaceService.FetchInterface<IGameDataManager>(GAMEDATA_INTERFACE_VERSION);

    g_pFilterMessageHook = hooksmanager->CreateFunctionHook();
    g_pFilterMessageHook->SetHookFunction(gamedata->GetSignatures()->Fetch("INetworkMessageProcessingPreFilter::FilterMessage"), (void*)FilterMessage);
    g_pFilterMessageHook->Enable();

    void* gameEventSystem = nullptr;
    s2binlib_find_vtable("engine2", "CGameEventSystem", &gameEventSystem);

    g_pPostEventAbstractHook = hooksmanager->CreateVFunctionHook();
    g_pPostEventAbstractHook->SetHookFunction(gameEventSystem, gamedata->GetOffsets()->Fetch("IGameEventSystem::PostEventAbstract"), (void*)PostEventAbstractHook, true);
    g_pPostEventAbstractHook->Enable();
}

void CNetMessages::Shutdown()
{
    g_pFilterMessageHook->Disable();
    g_pPostEventAbstractHook->Disable();

    auto hooksmanager = g_ifaceService.FetchInterface<IHooksManager>(HOOKSMANAGER_INTERFACE_VERSION);
    hooksmanager->DestroyFunctionHook(g_pFilterMessageHook);
    hooksmanager->DestroyVFunctionHook(g_pPostEventAbstractHook);
}

bool FilterMessage(INetworkMessageProcessingPreFilterCustom* client, CNetMessage* cMsg, INetChannel* netchan)
{
    if (!client) return reinterpret_cast<decltype(&FilterMessage)>(g_pFilterMessageHook->GetOriginal())(client, cMsg, netchan);
    if (!cMsg) return reinterpret_cast<decltype(&FilterMessage)>(g_pFilterMessageHook->GetOriginal())(client, cMsg, netchan);

    auto playerid = client->GetPlayerSlot().Get();
    int msgid = cMsg->GetNetMessage()->GetNetMessageInfo()->m_MessageId;

    for (const auto& [id, callback] : g_mClientMessageSendCallbacks) {
        auto res = callback(playerid, msgid, cMsg);
        if (res == 1) return true;
        else if (res == 2) break;
    }

    return reinterpret_cast<decltype(&FilterMessage)>(g_pFilterMessageHook->GetOriginal())(client, cMsg, netchan);
}

void PostEventAbstractHook(void* _this, CSplitScreenSlot nSlot, bool bLocalOnly, int nClientCount, const uint64* clients, INetworkMessageInternal* pEvent, const CNetMessage* pData, unsigned long nSize, NetChannelBufType_t bufType)
{
    if (bypassPostEventAbstractHook) return reinterpret_cast<decltype(&PostEventAbstractHook)>(g_pPostEventAbstractHook->GetOriginal())(_this, nSlot, bLocalOnly, nClientCount, clients, pEvent, pData, nSize, bufType);

    int msgid = pEvent->GetNetMessageInfo()->m_MessageId;
    CNetMessage* msg = const_cast<CNetMessage*>(pData);
    uint64_t* playermask = (uint64_t*)(clients);

    for (const auto& [id, callback] : g_mServerMessageSendCallbacks) {
        auto res = callback(playermask, msgid, msg);
        if (res == 1) return;
        else if (res == 2) break;
    }

    reinterpret_cast<decltype(&PostEventAbstractHook)>(g_pPostEventAbstractHook->GetOriginal())(_this, nSlot, bLocalOnly, nClientCount, clients, pEvent, pData, nSize, bufType);
}

uint64_t CNetMessages::AddServerMessageSendCallback(std::function<int(uint64_t*, int, void*)> callback)
{
    static uint64_t s_CallbackID = 0;
    g_mServerMessageSendCallbacks[s_CallbackID++] = callback;
    return s_CallbackID - 1;
}

void CNetMessages::RemoveServerMessageSendCallback(uint64_t callbackID)
{
    g_mServerMessageSendCallbacks.erase(callbackID);
}

uint64_t CNetMessages::AddClientMessageSendCallback(std::function<int(int, int, void*)> callback)
{
    static uint64_t s_CallbackID = 0;
    g_mClientMessageSendCallbacks[s_CallbackID++] = callback;
    return s_CallbackID - 1;
}

void CNetMessages::RemoveClientMessageSendCallback(uint64_t callbackID)
{
    g_mClientMessageSendCallbacks.erase(callbackID);
}